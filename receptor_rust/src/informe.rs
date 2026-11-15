//! Informe claro y conciso de lo que ocurre en la misión, inferido solo de la telemetría recibida.
//! Usa tendencias (regresión lineal) y los límites que envía la propia nave; no inventa diagnósticos.

use crate::estacion::{dia_de, utc_ahora, Estacion};
use crate::filtros::pendiente;
use serde_json::Value;
use std::fs;

/// Número con coma decimal.
pub fn n(x: f64, dec: usize) -> String {
    format!("{x:.dec$}").replace('.', ",")
}

fn f(v: &Value) -> f64 {
    v.as_f64().unwrap_or(0.0)
}

fn s(v: &Value) -> &str {
    v.as_str().unwrap_or("?")
}

fn serie(est: &Estacion, nombre: &str, clave: &str, dia: i64) -> Vec<(f64, f64)> {
    est.hist
        .get(nombre)
        .and_then(|h| h.get(clave))
        .map(|m| m.range(dia - 60..).map(|(d, v)| (*d as f64, *v)).collect())
        .unwrap_or_default()
}

fn ritmo_medio(est: &Estacion, trip: &[Value], clave: &str, dia: i64) -> Option<f64> {
    let r: Vec<f64> = trip.iter().filter_map(|c| pendiente(&serie(est, s(&c["nombre"]), clave, dia))).collect();
    if r.is_empty() { None } else { Some(r.iter().sum::<f64>() / r.len() as f64) }
}

fn promedio(trip: &[Value], clave: &str) -> f64 {
    let v: Vec<f64> = trip.iter().filter_map(|c| c[clave].as_f64()).collect();
    if v.is_empty() { 0.0 } else { v.iter().sum::<f64>() / v.len() as f64 }
}

fn alertas_ordenadas(d: &Value) -> Vec<Value> {
    let mut a = d["alertas"].as_array().cloned().unwrap_or_default();
    a.sort_by(|x, y| f(&y["severidad"]).partial_cmp(&f(&x["severidad"])).unwrap_or(std::cmp::Ordering::Equal));
    a
}

fn nombres(a: &Value) -> Vec<String> {
    a["tripulantes"].as_array().map(|v| v.iter().map(|x| s(x).to_string()).collect()).unwrap_or_default()
}

fn quien(a: &Value, total: usize) -> String {
    let t = nombres(a);
    if t.len() == total { "toda la tripulación".into() } else { t.join(", ") }
}

fn nivel(a: &Value) -> &'static str {
    if a["nivel"] == "CRITICO" { "crítico" } else { "precaución" }
}

pub fn estado_general(d: &Value) -> &'static str {
    let a = d["alertas"].as_array().cloned().unwrap_or_default();
    if a.iter().any(|x| x["nivel"] == "CRITICO") {
        "CRÍTICO"
    } else if !a.is_empty() {
        "PRECAUCIÓN"
    } else {
        "NOMINAL"
    }
}

fn inferencias(est: &Estacion, d: &Value) -> Vec<String> {
    let mut out = Vec::new();
    let trip = d["tripulacion"].as_array().cloned().unwrap_or_default();
    if trip.is_empty() {
        return out;
    }
    let dia = dia_de(d);
    let total = f(&d["dia_total"]);
    let restan = (total - dia as f64).max(0.0);

    if let Some(p) = &est.previo {
        if p["fase"] != d["fase"] {
            out.push(format!(
                "**Cambio de fase:** la nave pasó de *{}* a *{}* ({} g, {} mSv/día).",
                s(&p["fase"]), s(&d["fase"]), n(f(&d["gravedad_g"]), 2), n(f(&d["radiacion_msv_dia"]), 2)
            ));
        }
    }

    // Radiación
    let limite = d["limite_dosis_msv"].as_f64().unwrap_or(600.0);
    let peor = trip.iter().max_by(|a, b| f(&a["dosis_msv"]).partial_cmp(&f(&b["dosis_msv"])).unwrap()).unwrap();
    let dosis = f(&peor["dosis_msv"]);
    let nombre = s(&peor["nombre"]);
    let ritmo = pendiente(&serie(est, nombre, "dosis_msv", dia)).unwrap_or(f(&d["radiacion_msv_dia"]));
    if d["enlace"]["distancia_km"].as_f64().unwrap_or(1.0) <= 0.0 {
        out.push(format!(
            "**Radiación:** en Tierra (solo fondo natural, {} mSv/día); dosis acumulada máxima {} de {} mSv.",
            n(f(&d["radiacion_msv_dia"]), 3), n(dosis, 0), n(limite, 0)
        ));
    } else if dosis >= limite {
        out.push(format!("**Radiación:** {nombre} superó el límite de carrera ({} de {} mSv).", n(dosis, 0), n(limite, 0)));
    } else if ritmo > 0.0 {
        let dia_lim = dia as f64 + (limite - dosis) / ritmo;
        if total > 0.0 && dia_lim <= total {
            out.push(format!(
                "**Radiación:** la dosis sube {} mSv/día ({nombre}: {} de {} mSv). A este ritmo alcanzaría el límite de carrera cerca del día {}, **antes del fin de la misión** (día {}).",
                n(ritmo, 2), n(dosis, 0), n(limite, 0), n(dia_lim, 0), n(total, 0)
            ));
        } else {
            out.push(format!(
                "**Radiación:** la dosis sube {} mSv/día ({nombre}: {} mSv). Proyección al final de la misión: {} de {} mSv (dentro del límite).",
                n(ritmo, 2), n(dosis, 0), n(dosis + ritmo * restan, 0), n(limite, 0)
            ));
        }
    }

    // Hueso y músculo (promedio de la tripulación)
    for (clave, titulo) in [("hueso_pct", "Densidad ósea"), ("musculo_pct", "Masa muscular")] {
        let prom = promedio(&trip, clave);
        match ritmo_medio(est, &trip, clave, dia) {
            None => out.push(format!("**{titulo}:** promedio {} % de su valor inicial.", n(prom, 1))),
            Some(r) if r < -0.001 => out.push(format!(
                "**{titulo}:** promedio {} %; baja {} % por mes. Si sigue igual, terminaría la misión cerca de {} %.",
                n(prom, 1), n(-r * 30.0, 2), n(prom + r * restan, 1)
            )),
            Some(r) if r > 0.001 => out.push(format!(
                "**{titulo}:** promedio {} % y en recuperación (+{} %/mes).", n(prom, 1), n(r * 30.0, 2)
            )),
            Some(_) => out.push(format!("**{titulo}:** estable en {} %.", n(prom, 1))),
        }
    }

    // Visión (SANS)
    let sans = trip.iter().map(|c| f(&c["sans"])).fold(0.0, f64::max);
    let tend = match ritmo_medio(est, &trip, "sans", dia) {
        None => "",
        Some(r) if r > 1e-4 => " y en aumento",
        Some(r) if r < -1e-4 => " y bajando",
        Some(_) => " y estable",
    };
    out.push(format!("**Visión (SANS):** índice máximo {} (0 = normal, 1 = daño máximo){tend}.", n(sans, 2)));

    // Tormentas solares
    let tormentas = d["tormentas_solares"].as_i64().unwrap_or(0);
    match est.tormentas_informadas {
        Some(t) if tormentas > t => out.push(format!(
            "**Tormentas solares:** {} nueva(s) desde el último informe ({tormentas} en total). {}",
            tormentas - t,
            if d["contramedidas"]["refugio"].as_bool().unwrap_or(false) { "La tripulación usa el refugio blindado." } else { "Sin refugio activo." }
        )),
        _ if d["tormenta_activa"].as_bool().unwrap_or(false) => {
            out.push("**Tormenta solar en curso:** aumentan la dosis y el ruido del enlace.".into())
        }
        _ => {}
    }

    // Sueño, estrés y otros (solo si la nave los marca como problema)
    let alertas = alertas_ordenadas(d);
    let buscar = |sis: &str| alertas.iter().find(|a| a["sistema"] == sis);
    for sis in ["Sueño", "Estrés psicológico", "Función renal", "Capacidad aeróbica", "Cardiovascular"] {
        if let Some(a) = buscar(sis) {
            out.push(format!("**{sis}:** {} en {} ({}).", nivel(a), quien(a, trip.len()), s(&a["detalle"])));
        }
    }

    // Cabina
    let cab = &d["cabina"];
    let aire = match buscar("Calidad del aire") {
        Some(a) => format!("; **alerta de calidad del aire** ({}).", nivel(a)),
        None => "; sin alerta de calidad del aire.".into(),
    };
    out.push(format!(
        "**Cabina:** CO₂ medio {} mmHg (máx. {}), O₂ {} %, {} °C{aire}",
        n(f(&cab["co2_mmhg"]), 1), n(f(&cab["co2_max_mmhg"]), 1), n(f(&cab["o2_pct"]), 1), n(f(&cab["temp_c"]), 1)
    ));
    out
}

fn conclusion(d: &Value) -> String {
    let alertas = alertas_ordenadas(d);
    let total = d["tripulacion"].as_array().map(|a| a.len()).unwrap_or(0);
    if alertas.is_empty() {
        return "Tripulación en estado nominal. La telemetría no muestra problemas que requieran acción.".into();
    }
    let partes: Vec<String> = alertas
        .iter()
        .take(2)
        .map(|a| format!("{} ({}, {})", s(&a["sistema"]), nivel(a), quien(a, total)))
        .collect();
    let mut activas = Vec::new();
    if let Some(cm) = d["contramedidas"].as_object() {
        for (k, v) in cm {
            if k != "gravedad_artificial_g" {
                if let Some(x) = v.as_f64() {
                    activas.push(format!("{k} {} %", (x * 100.0).round()));
                }
            }
        }
    }
    let refugio = if d["contramedidas"]["refugio"].as_bool().unwrap_or(false) { ", refugio solar activo" } else { "" };
    format!(
        "Prioridad para el equipo médico en Tierra: {}. Contramedidas reportadas por la nave: {}{refugio}.",
        partes.join(" y "), activas.join(", ")
    )
}

fn distancia(e: &Value) -> String {
    let km = f(&e["distancia_km"]);
    if km <= 0.0 {
        "en Tierra".into()
    } else if km >= 1.5e6 {
        format!("{} UA ({} millones de km)", n(f(&e["distancia_ua"]), 2), n(km / 1e6, 0))
    } else {
        format!("{} mil km", n(km / 1000.0, 0))
    }
}

fn luz(seg: f64) -> String {
    if seg >= 60.0 { format!("{} min", n(seg / 60.0, 1)) } else { format!("{} s", n(seg, 2)) }
}

/// Escribe el informe. Devuelve (titular, ruta).
pub fn generar(est: &mut Estacion, motivo: &str, estacion: &str) -> (String, String) {
    let d = est.ultimo.clone().unwrap_or(Value::Null);
    let trip = d["tripulacion"].as_array().cloned().unwrap_or_default();
    let dia = dia_de(&d);
    let general = estado_general(&d);
    let alertas = alertas_ordenadas(&d);
    let criticas: Vec<&Value> = alertas.iter().filter(|a| a["nivel"] == "CRITICO").collect();
    let base: Vec<&Value> = if criticas.is_empty() { alertas.iter().collect() } else { criticas.clone() };
    let resumen = base.iter().take(3).map(|a| format!("{} en {}", s(&a["sistema"]), quien(a, trip.len()))).collect::<Vec<_>>().join("; ");
    let titular = format!("Día {dia} · {} · {general}{}", s(&d["fase"]), if resumen.is_empty() { String::new() } else { format!(": {resumen}") });

    let mut l: Vec<String> = Vec::new();
    l.push(format!("# Informe de telemetría · {} · Día {dia} de {}", s(&d["nave"]), n(f(&d["dia_total"]), 0)));
    l.push(String::new());
    l.push(format!(
        "**Misión:** {} · **Fase:** {} ({} g, {} mSv/día)  ",
        s(&d["mision"]), s(&d["fase"]), n(f(&d["gravedad_g"]), 2), n(f(&d["radiacion_msv_dia"]), 2)
    ));
    l.push(format!("**Generado:** {} · **Estación:** {estacion} · **Motivo:** {motivo}", utc_ahora(false)));
    l.push(String::new());
    l.push("## Resumen".into());
    let detalle = if !criticas.is_empty() {
        format!(" {} problema(s) crítico(s): {resumen}.", criticas.len())
    } else if !resumen.is_empty() {
        format!(" {resumen}.")
    } else {
        String::new()
    };
    l.push(format!("**Estado general: {general}.**{detalle}"));
    l.push(String::new());
    l.push(conclusion(&d));
    l.push(String::new());
    l.push("## Qué está pasando".into());
    for x in inferencias(est, &d) {
        l.push(format!("- {x}"));
    }
    l.push(String::new());
    l.push("## Tripulación".into());
    l.push("| Tripulante | Índice | Hueso | Músculo | VO₂ máx | SANS | Dosis (mSv) | FC* | SpO₂* | Problema principal |".into());
    l.push("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |".into());
    for c in &trip {
        let vf = &c["vitales_filtrados"];
        let nom = s(&c["nombre"]);
        let prob = alertas
            .iter()
            .find(|a| nombres(a).iter().any(|x| x == nom))
            .map(|a| format!("{} ({})", s(&a["sistema"]), nivel(a)))
            .unwrap_or_else(|| "—".into());
        l.push(format!(
            "| {nom} ({}) | {} | {} % | {} % | {} % | {} | {} | {} | {} % | {prob} |",
            c["rol"].as_str().unwrap_or(""), n(f(&c["indice_salud"]), 0), n(f(&c["hueso_pct"]), 1), n(f(&c["musculo_pct"]), 1),
            n(f(&c["vo2_pct"]), 1), n(f(&c["sans"]), 2), n(f(&c["dosis_msv"]), 0), n(f(&vf["fc"]), 0), n(f(&vf["spo2"]), 0)
        ));
    }
    l.push(String::new());
    l.push("\\* Signos vitales después del filtro de picos.".into());
    l.push(String::new());

    let e = &d["enlace"];
    let st = &est.stats;
    l.push("## Enlace espacio–Tierra y calidad de los datos".into());
    l.push(format!(
        "- **Distancia:** {} · **retraso de la luz:** {} · **vía:** {}",
        distancia(e), luz(f(&e["retraso_luz_s"])), s(&e["via"])
    ));
    if f(&e["distancia_km"]) > 0.0 {
        l.push(format!(
            "- **Señal:** Eb/N0 {} dB · tasa de error de bit estimada {:.1e} · Sol a {}° de la línea de visión",
            n(f(&e["ebn0_db"]), 1), f(&e["ber"]), n(f(&e["angulo_sol_grados"]), 0)
        ));
    }
    if let Some(fr) = e["fuentes_ruido"].as_array() {
        if !fr.is_empty() {
            l.push(format!("- **Fuentes de ruido:** {}", fr.iter().map(|x| s(x)).collect::<Vec<_>>().join("; ")));
        }
    }
    l.push(format!(
        "- **Tramas:** {} recibidas · {} válidas · {} reparadas ({} bits corregidos por Hamming) · {} descartadas por ruido (CRC) · {} perdidas en el camino · {} rechazadas por clave inválida · {} repetidas · **calidad {} %**",
        st.recibidas, st.validas, st.tramas_corregidas, st.bits_corregidos, st.descartadas_ruido, st.perdidas_canal(),
        st.rechazadas_clave, st.repetidas, n(st.calidad(), 1)
    ));
    l.push("- **Seguridad:** AES-256 + firma HMAC-SHA256 verificada en cada trama válida.".into());
    if est.picos.is_empty() {
        l.push("- **Filtro de picos:** sin lecturas anómalas.".into());
    } else {
        l.push(format!(
            "- **Filtro de picos:** {} lectura(s) descartada(s) (posibles impactos de radiación en los sensores). Últimas:",
            st.picos_filtrados
        ));
        for p in est.picos.iter().rev().take(5).collect::<Vec<_>>().into_iter().rev() {
            l.push(format!("  - Día {}, {}, {}: {} → {} ({})", p.dia, p.quien, p.variable, n(p.bruto, 1), n(p.aceptado, 1), p.motivo));
        }
    }
    l.push(String::new());
    l.push("_Simulación educativa NASA Space Apps (equipo AEGIS). No es un diagnóstico médico._".into());

    let texto = l.join("\n") + "\n";
    let _ = fs::create_dir_all(&est.carpeta);
    let ruta = est.carpeta.join(format!("informe_dia_{dia:04}.md"));
    let _ = fs::write(&ruta, &texto);
    let _ = fs::write(est.carpeta.join("ultimo_informe.md"), &texto);
    est.marcar_informado();
    (titular, ruta.display().to_string())
}
