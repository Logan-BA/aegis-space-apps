//! Estado de la estación terrena: recibe tramas, mide la calidad del enlace, filtra picos y guarda historia.

use crate::filtros::{FiltroHampel, SENSORES};
use crate::protocolo::{self, ErrorTrama};
use serde_json::{json, Value};
use std::collections::{BTreeMap, HashMap, HashSet, VecDeque};
use std::fs::{self, OpenOptions};
use std::io::Write;
use std::path::PathBuf;
use std::time::{Instant, SystemTime, UNIX_EPOCH};

pub const METRICAS: [&str; 8] =
    ["indice_salud", "hueso_pct", "musculo_pct", "vo2_pct", "sans", "dosis_msv", "estres", "deuda_sueno_h"];

#[derive(Default)]
pub struct Estadisticas {
    pub recibidas: u64,
    pub validas: u64,
    pub bits_corregidos: u64,
    pub tramas_corregidas: u64,
    pub descartadas_ruido: u64,
    pub rechazadas_clave: u64,
    pub invalidas: u64,
    pub repetidas: u64,
    pub huecos_secuencia: u64,
    pub picos_filtrados: u64,
    pub otra_sesion: u64,
}

impl Estadisticas {
    /// Tramas que nunca llegaron (huecos en la secuencia que no explican las descartadas).
    pub fn perdidas_canal(&self) -> u64 {
        self.huecos_secuencia.saturating_sub(self.descartadas_ruido + self.rechazadas_clave + self.invalidas)
    }
    pub fn calidad(&self) -> f64 {
        let total = self.validas + self.descartadas_ruido + self.perdidas_canal();
        if total == 0 { 0.0 } else { 100.0 * self.validas as f64 / total as f64 }
    }
}

pub struct Resultado {
    pub ok: bool,
    pub mensaje: String,
    pub motivo_informe: Option<String>,
}

pub struct Pico {
    pub dia: i64,
    pub quien: String,
    pub variable: String,
    pub bruto: f64,
    pub aceptado: f64,
    pub motivo: String,
}

pub struct Estacion {
    pub k_cif: [u8; 32],
    pub k_mac: [u8; 32],
    pub carpeta: PathBuf,
    pub cada_dias: i64,
    pub stats: Estadisticas,
    pub ultimo: Option<Value>,
    pub previo: Option<Value>,
    pub hist: HashMap<String, HashMap<String, BTreeMap<i64, f64>>>,
    pub picos: VecDeque<Pico>,
    pub tormentas_informadas: Option<i64>,
    sesion: Option<u32>,
    ultima_seq: Option<u32>,
    t_ultimo: Option<Instant>,
    filtros: HashMap<(String, String), FiltroHampel>,
    dia_informe: Option<i64>,
    criticas_informadas: HashSet<String>,
    fase_informada: Option<String>,
    terminada_informada: bool,
}

pub fn dia_de(d: &Value) -> i64 {
    d["dia"].as_f64().unwrap_or(0.0) as i64
}

fn criticas(d: &Value) -> HashSet<String> {
    d["alertas"]
        .as_array()
        .map(|a| {
            a.iter()
                .filter(|x| x["nivel"] == "CRITICO")
                .filter_map(|x| x["sistema"].as_str().map(String::from))
                .collect()
        })
        .unwrap_or_default()
}

impl Estacion {
    pub fn new(k_cif: [u8; 32], k_mac: [u8; 32], carpeta: PathBuf, cada_dias: i64) -> Self {
        let _ = fs::create_dir_all(&carpeta);
        Self {
            k_cif, k_mac, carpeta, cada_dias,
            stats: Estadisticas::default(),
            ultimo: None, previo: None,
            hist: HashMap::new(),
            picos: VecDeque::new(),
            tormentas_informadas: None,
            sesion: None, ultima_seq: None, t_ultimo: None,
            filtros: HashMap::new(),
            dia_informe: None,
            criticas_informadas: HashSet::new(),
            fase_informada: None,
            terminada_informada: false,
        }
    }

    pub fn recibir(&mut self, bloque: &[u8]) -> Resultado {
        self.stats.recibidas += 1;
        let t = match protocolo::abrir(bloque, &self.k_cif, &self.k_mac) {
            Ok(t) => t,
            Err(ErrorTrama::Canal(m)) => {
                self.stats.descartadas_ruido += 1;
                return Resultado { ok: false, mensaje: format!("trama descartada por ruido del canal ({m})"), motivo_informe: None };
            }
            Err(ErrorTrama::Autenticacion(m)) => {
                self.stats.rechazadas_clave += 1;
                return Resultado { ok: false, mensaje: format!("trama RECHAZADA: {m}"), motivo_informe: None };
            }
            Err(ErrorTrama::Formato(m)) => {
                self.stats.invalidas += 1;
                return Resultado { ok: false, mensaje: format!("trama inválida ({m})"), motivo_informe: None };
            }
        };

        // Una sola nave a la vez: si otra sesión transmite mientras la actual sigue activa, se ignora
        if let (Some(s), Some(tu)) = (self.sesion, self.t_ultimo) {
            if s != t.sesion && tu.elapsed().as_secs_f64() < 10.0 {
                self.stats.otra_sesion += 1;
                return Resultado {
                    ok: false,
                    mensaje: format!("trama de otra sesión ({:08X}) ignorada: la estación sigue a la nave activa", t.sesion),
                    motivo_informe: None,
                };
            }
        }
        // Secuencia: tramas perdidas y repeticiones (ataque de repetición)
        if Some(t.sesion) != self.sesion {
            self.sesion = Some(t.sesion);
            self.ultima_seq = None;
        } else if let Some(u) = self.ultima_seq {
            if t.secuencia <= u {
                self.stats.repetidas += 1;
                return Resultado { ok: false, mensaje: format!("trama #{} repetida: descartada", t.secuencia), motivo_informe: None };
            }
            self.stats.huecos_secuencia += (t.secuencia - u - 1) as u64;
        }
        self.ultima_seq = Some(t.secuencia);

        let mut d = t.datos;
        if let Some(u) = &self.ultimo {
            if dia_de(&d) < dia_de(u) {
                // misión reiniciada
                self.hist.clear();
                self.filtros.clear();
                self.picos.clear();
                self.previo = None;
                self.dia_informe = None;
                self.criticas_informadas.clear();
                self.tormentas_informadas = None;
                self.terminada_informada = false;
            }
        }
        self.t_ultimo = Some(Instant::now());
        self.stats.validas += 1;
        self.stats.bits_corregidos += t.bits_corregidos as u64;
        if t.bits_corregidos > 0 {
            self.stats.tramas_corregidas += 1;
        }
        self.filtrar(&mut d);
        self.guardar_csv(&d, t.secuencia, t.bits_corregidos);
        let motivo = self.motivo_informe(&d);
        let corr = if t.bits_corregidos > 0 { format!(" · {} bits corregidos", t.bits_corregidos) } else { String::new() };
        let mensaje = format!("trama #{} OK · día {}{}", t.secuencia, dia_de(&d), corr);
        let _ = t.t_ms;
        self.ultimo = Some(d);
        Resultado { ok: true, mensaje, motivo_informe: motivo }
    }

    fn filtrar(&mut self, d: &mut Value) {
        let dia = dia_de(d);
        let Some(trip) = d.get_mut("tripulacion").and_then(Value::as_array_mut) else { return };
        for c in trip.iter_mut() {
            let nombre = c["nombre"].as_str().unwrap_or("?").to_string();
            let mut filtrados = serde_json::Map::new();
            for (var, mn, mx, piso) in SENSORES {
                let Some(x) = c["vitales"][var].as_f64() else { continue };
                let f = self
                    .filtros
                    .entry((nombre.clone(), var.to_string()))
                    .or_insert_with(|| FiltroHampel::new(mn, mx, piso));
                let (aceptado, motivo) = f.filtrar(x);
                filtrados.insert(var.to_string(), json!(aceptado));
                if let Some(m) = motivo {
                    self.stats.picos_filtrados += 1;
                    self.picos.push_back(Pico { dia, quien: nombre.clone(), variable: var.to_string(), bruto: x, aceptado, motivo: m });
                    if self.picos.len() > 40 {
                        self.picos.pop_front();
                    }
                }
            }
            let h = self.hist.entry(nombre.clone()).or_default();
            for m in METRICAS {
                if let Some(v) = c[m].as_f64() {
                    h.entry(m.to_string()).or_default().insert(dia, v);
                }
            }
            for var in ["fc", "spo2"] {
                if let Some(v) = filtrados.get(var).and_then(Value::as_f64) {
                    h.entry(var.to_string()).or_default().insert(dia, v);
                }
            }
            c["vitales_filtrados"] = Value::Object(filtrados);
        }
    }

    fn guardar_csv(&self, d: &Value, seq: u32, corregidos: usize) {
        let ruta = self.carpeta.join("telemetria.csv");
        let nuevo = !ruta.exists();
        let Ok(mut f) = OpenOptions::new().create(true).append(true).open(&ruta) else { return };
        if nuevo {
            let _ = writeln!(f, "recibido_utc,secuencia,dia,hora,fase,tripulante,indice_salud,hueso_pct,musculo_pct,vo2_pct,sans,dosis_msv,estres,fc_filtrada,spo2_filtrada,temp_filtrada,co2_mmhg,bits_corregidos");
        }
        let ahora = utc_ahora(true);
        let num = |v: &Value| v.as_f64().map(|x| x.to_string()).unwrap_or_default();
        if let Some(trip) = d["tripulacion"].as_array() {
            for c in trip {
                let vf = &c["vitales_filtrados"];
                let _ = writeln!(
                    f, "{},{},{},{},\"{}\",\"{}\",{},{},{},{},{},{},{},{},{},{},{},{}",
                    ahora, seq, num(&d["dia"]), num(&d["hora"]), d["fase"].as_str().unwrap_or(""), c["nombre"].as_str().unwrap_or(""),
                    num(&c["indice_salud"]), num(&c["hueso_pct"]), num(&c["musculo_pct"]), num(&c["vo2_pct"]), num(&c["sans"]),
                    num(&c["dosis_msv"]), num(&c["estres"]), num(&vf["fc"]), num(&vf["spo2"]), num(&vf["temp_c"]),
                    num(&d["cabina"]["co2_mmhg"]), corregidos
                );
            }
        }
    }

    fn motivo_informe(&self, d: &Value) -> Option<String> {
        let dia = dia_de(d);
        let Some(di) = self.dia_informe else { return Some("primer contacto con la nave".into()) };
        if d["mision_terminada"].as_bool().unwrap_or(false) && !self.terminada_informada {
            return Some("fin de la misión".into());
        }
        let mut nuevas: Vec<String> = criticas(d).difference(&self.criticas_informadas).cloned().collect();
        if !nuevas.is_empty() {
            nuevas.sort();
            return Some(format!("nueva alerta crítica: {}", nuevas.join(", ")));
        }
        let tormentas = d["tormentas_solares"].as_i64().unwrap_or(0);
        if let Some(t) = self.tormentas_informadas {
            if tormentas > t {
                return Some("tormenta solar detectada".into());
            }
        }
        if let Some(f) = &self.fase_informada {
            if d["fase"].as_str() != Some(f.as_str()) {
                return Some(format!("cambio de fase: {}", d["fase"].as_str().unwrap_or("?")));
            }
        }
        if self.cada_dias > 0 && dia - di >= self.cada_dias {
            return Some(format!("informe periódico (cada {} días)", self.cada_dias));
        }
        None
    }

    pub fn marcar_informado(&mut self) {
        let Some(d) = self.ultimo.clone() else { return };
        self.dia_informe = Some(dia_de(&d));
        self.criticas_informadas = criticas(&d);
        self.tormentas_informadas = d["tormentas_solares"].as_i64();
        self.fase_informada = d["fase"].as_str().map(String::from);
        self.terminada_informada = d["mision_terminada"].as_bool().unwrap_or(false);
        self.previo = Some(d);
    }
}

/// Fecha y hora UTC actuales ("2026-11-14T19:50:12Z" o "2026-11-14 19:50:12 UTC").
pub fn utc_ahora(iso: bool) -> String {
    let s = SystemTime::now().duration_since(UNIX_EPOCH).map(|d| d.as_secs() as i64).unwrap_or(0);
    let (dias, resto) = (s.div_euclid(86_400), s.rem_euclid(86_400));
    // algoritmo "civil_from_days" de Howard Hinnant
    let z = dias + 719_468;
    let era = z.div_euclid(146_097);
    let doe = z - era * 146_097;
    let yoe = (doe - doe / 1460 + doe / 36_524 - doe / 146_096) / 365;
    let doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
    let mp = (5 * doy + 2) / 153;
    let d = doy - (153 * mp + 2) / 5 + 1;
    let m = if mp < 10 { mp + 3 } else { mp - 9 };
    let y = yoe + era * 400 + if m <= 2 { 1 } else { 0 };
    let (h, mi, se) = (resto / 3600, (resto % 3600) / 60, resto % 60);
    if iso {
        format!("{y:04}-{m:02}-{d:02}T{h:02}:{mi:02}:{se:02}Z")
    } else {
        format!("{y:04}-{m:02}-{d:02} {h:02}:{mi:02}:{se:02} UTC")
    }
}
