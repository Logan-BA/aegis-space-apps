// Panel de control AEGIS: consulta la estación terrena y dibuja el estado de la misión (sin librerías externas).
"use strict";

const $ = (id) => document.getElementById(id);
const COLORES = ["#5b9bd8", "#d6a24e", "#5fb27f", "#a77ad0"];
let historia = {};
let ultimoEstado = null;

const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
const md = (s) => esc(s).replace(/\*\*(.+?)\*\*/g, "<b>$1</b>").replace(/\*(.+?)\*/g, "<i>$1</i>");
const n = (x, d = 1) => (x === undefined || x === null || isNaN(x)) ? "—" : Number(x).toLocaleString("es", { minimumFractionDigits: d, maximumFractionDigits: d });
const clase = (nivel) => nivel === "CRITICO" || nivel === "CRÍTICO" ? "critico" : nivel === "PRECAUCION" || nivel === "PRECAUCIÓN" ? "precaucion" : "nominal";

async function json(url, opciones) {
  const r = await fetch(url, { cache: "no-store", ...opciones });
  const cuerpo = await r.json().catch(() => ({}));
  if (!r.ok && !cuerpo.error) throw new Error(`HTTP ${r.status}`);
  return cuerpo;
}

function aviso(texto, error = false) {
  const a = $("aviso");
  a.textContent = texto;
  a.className = "aviso" + (error ? " error" : "");
  a.hidden = false;
  clearTimeout(aviso.t);
  aviso.t = setTimeout(() => (a.hidden = true), 6000);
}

// ------------------------------------------------------------------ estado

function pintarEstado(e) {
  ultimoEstado = e;
  $("estacion").textContent = e.estacion;
  $("reloj").textContent = e.hora_utc;
  const d = e.datos;
  const pill = $("enlace-pill");
  if (e.conectada && e.segundos_sin_datos !== null && e.segundos_sin_datos < 6) {
    pill.className = "pill ok"; pill.textContent = "● NAVE EN LÍNEA";
  } else if (e.conectada) {
    pill.className = "pill aviso"; pill.textContent = `● SIN DATOS ${Math.round(e.segundos_sin_datos ?? 0)} s`;
  } else {
    pill.className = "pill gris"; pill.textContent = d ? "○ NAVE DESCONECTADA" : "○ SIN SEÑAL";
  }
  $("espera").hidden = !!d;
  $("url-ws").textContent = `ws://${location.hostname}:8765`;
  if (!d) return;

  const s = e.stats, enl = d.enlace || {};
  $("mision").textContent = `${d.nave} · ${d.mision}`;
  $("fase").innerHTML = `<b>Día ${d.dia}</b> · ${esc(d.fase)} · ${n(d.gravedad_g, 2)} g · ${n(d.radiacion_msv_dia, 2)} mSv/día`;

  const k = $("k-estado"), general = e.estado_general || "NOMINAL";
  k.className = "kpi " + clase(general);
  k.querySelector(".valor").textContent = general;
  const crit = (d.alertas || []).filter((a) => a.nivel === "CRITICO").length;
  k.querySelector(".nota").textContent = crit ? `${crit} problema(s) crítico(s)` : `${(d.alertas || []).length} en vigilancia`;

  $("k-dia").textContent = `${d.dia} / ${n(d.dia_total, 0)}`;
  $("k-dia-bar").style.width = `${Math.min(100, (100 * d.dia) / Math.max(1, d.dia_total))}%`;
  $("k-dia-nota").textContent = d.mision_terminada ? "misión completada" : `faltan ${n(Math.max(0, d.dia_total - d.dia), 0)} días`;

  const km = enl.distancia_km || 0;
  $("k-dist").textContent = km <= 0 ? "En Tierra" : km >= 1.5e6 ? `${n(enl.distancia_ua, 2)} UA` : `${n(km / 1000, 0)} mil km`;
  const luz = enl.retraso_luz_s || 0;
  $("k-luz").textContent = km <= 0 ? "sin retraso" : luz >= 60 ? `la señal tarda ${n(luz / 60, 1)} min` : `la señal tarda ${n(luz, 2)} s`;

  const kc = $("k-calidad").parentElement;
  kc.className = "kpi " + (s.calidad >= 90 ? "nominal" : s.calidad >= 60 ? "precaucion" : "critico");
  $("k-calidad").textContent = `${n(s.calidad, 1)} %`;
  $("k-bits").textContent = `${s.bits_corregidos.toLocaleString("es")} bits corregidos · ${s.descartadas_ruido} descartadas`;

  $("k-tormentas").textContent = d.tormentas_solares;
  $("k-tormenta-nota").textContent = d.tormenta_activa ? "TORMENTA EN CURSO" : (d.contramedidas?.refugio ? "refugio solar listo" : "sin refugio");
  $("k-tormentas").parentElement.className = "kpi " + (d.tormenta_activa ? "critico" : "");
  $("k-picos").textContent = s.picos_filtrados;

  pintarTripulacion(d);
  $("conclusion").innerHTML = md(e.conclusion);
  $("inferencias").innerHTML = (e.inferencias || []).map((x) => `<li>${md(x)}</li>`).join("");
  $("actualizado").textContent = e.segundos_sin_datos !== null ? `hace ${n(e.segundos_sin_datos, 0)} s` : "";

  const al = (d.alertas || []).slice().sort((a, b) => b.severidad - a.severidad);
  $("n-alertas").textContent = al.length ? `${al.length}` : "ninguna";
  const total = (d.tripulacion || []).length;
  $("alertas").innerHTML = al.length ? al.map((a) => `<li><span class="nivel ${clase(a.nivel)}">${a.nivel === "CRITICO" ? "CRÍTICO" : "PRECAUCIÓN"}</span>
      <div><b>${esc(a.sistema)}</b><span class="tenue">${a.tripulantes.length === total ? "Toda la tripulación" : esc(a.tripulantes.join(", "))} · ${esc(a.detalle)}</span></div></li>`).join("")
    : `<li class="vacio">Sin alertas: tripulación en estado nominal.</li>`;

  const filas = [
    ["Vía", enl.via], ["Distancia", km <= 0 ? "en Tierra" : `${n(km / 1e6, 1)} millones de km`],
    ["Retraso de la luz", km <= 0 ? "—" : luz >= 60 ? `${n(luz / 60, 1)} min` : `${n(luz, 2)} s`],
  ];
  if (km > 0) filas.push(["Señal Eb/N0", `${n(enl.ebn0_db, 1)} dB`], ["Error de bit estimado", Number(enl.ber || 0).toExponential(1)],
    ["Sol respecto a la nave", `${n(enl.angulo_sol_grados, 0)}°`]);
  filas.push(["Tramas válidas", `${s.validas} de ${s.recibidas}`], ["Reparadas por Hamming", `${s.tramas_corregidas}`],
    ["Perdidas en el camino", `${s.perdidas_canal}`], ["Rechazadas (clave)", `${s.rechazadas_clave}`]);
  $("enlace").innerHTML = filas.map(([a, b]) => `<dt>${esc(a)}</dt><dd>${esc(b)}</dd>`).join("");
  $("ruido").innerHTML = (enl.fuentes_ruido || []).map((f) => `<li>${esc(f)}</li>`).join("");

  $("picos").innerHTML = (e.picos || []).length ? e.picos.map((p) => `<li><span class="tenue">Día ${p.dia}</span>
      <span>${esc(p.tripulante)} · ${esc(p.sensor)} <span class="del">${n(p.leido, 1)}</span> → ${n(p.aceptado, 1)}</span>
      <span class="tenue" title="${esc(p.motivo)}">${p.motivo.startsWith("fuera") ? "imposible" : "pico"}</span></li>`).join("")
    : `<li class="vacio">Sin lecturas anómalas.</li>`;
}

function barra(etiqueta, valor, min, max, texto, color) {
  const p = Math.max(0, Math.min(100, (100 * (valor - min)) / (max - min)));
  return `<div class="metrica"><span class="tenue">${etiqueta}</span><div class="pista"><span style="width:${p}%;background:${color}"></span></div><span class="num">${texto}</span></div>`;
}

function pintarTripulacion(d) {
  const al = (d.alertas || []).slice().sort((a, b) => b.severidad - a.severidad);
  const limite = d.limite_dosis_msv || 600;
  $("tripulacion").innerHTML = (d.tripulacion || []).map((c) => {
    const suya = al.find((a) => a.tripulantes.includes(c.nombre));
    const nivel = suya ? clase(suya.nivel) : "nominal";
    const v = c.vitales_filtrados || c.vitales || {};
    const col = (bueno) => (bueno ? "var(--ok)" : "var(--aviso)");
    return `<article class="tripulante ${nivel}">
      <header><div><h3>${esc(c.nombre)}</h3><p class="rol">${esc(c.rol)} · ${c.edad} años${c.en_eva ? " · en caminata exterior" : ""}</p></div>
        <div class="indice"><b>${n(c.indice_salud, 0)}</b><span>índice de salud /100</span></div></header>
      <p class="problema ${nivel}">${suya ? `${esc(suya.sistema)} · ${suya.nivel === "CRITICO" ? "crítico" : "precaución"}` : "Sin problemas activos"}</p>
      <div class="metricas">
        ${barra("Hueso", c.hueso_pct, 75, 100, `${n(c.hueso_pct)} %`, col(c.hueso_pct > 92))}
        ${barra("Músculo", c.musculo_pct, 70, 100, `${n(c.musculo_pct)} %`, col(c.musculo_pct > 88))}
        ${barra("VO₂ máx", c.vo2_pct, 70, 100, `${n(c.vo2_pct)} %`, col(c.vo2_pct > 88))}
        ${barra("SANS (visión)", c.sans, 0, 1, n(c.sans, 2), c.sans >= 0.65 ? "var(--peligro)" : c.sans >= 0.35 ? "var(--aviso)" : "var(--ok)")}
        ${barra("Dosis", c.dosis_msv, 0, limite, `${n(c.dosis_msv, 0)} mSv`, c.dosis_msv / limite >= 0.65 ? "var(--peligro)" : c.dosis_msv / limite >= 0.35 ? "var(--aviso)" : "var(--ok)")}
      </div>
      <div class="vitales">
        <div class="vital"><small>FC</small><b>${n(v.fc, 0)}</b> <small>lpm</small></div>
        <div class="vital"><small>SpO₂</small><b>${n(v.spo2, 0)}</b> <small>%</small></div>
        <div class="vital"><small>Temp.</small><b>${n(v.temp_c, 1)}</b> <small>°C</small></div>
        <div class="vital"><small>Presión</small><b>${n(v.pas, 0)}/${n(v.pad, 0)}</b></div>
      </div></article>`;
  }).join("");
}

// ------------------------------------------------------------------ gráficos

function grafico(canvas, series, { limite = null, dec = 1 } = {}) {
  const dpr = window.devicePixelRatio || 1;
  const w = canvas.clientWidth, h = canvas.clientHeight;
  if (!w || !h) return;
  canvas.width = w * dpr; canvas.height = h * dpr;
  const g = canvas.getContext("2d");
  g.setTransform(dpr, 0, 0, dpr, 0, 0);
  g.clearRect(0, 0, w, h);
  const pts = series.flatMap((s) => s.puntos);
  g.font = "11px Segoe UI, system-ui, sans-serif";
  if (pts.length < 2) {
    g.fillStyle = "#8f9bad"; g.textAlign = "center";
    g.fillText("Esperando datos…", w / 2, h / 2);
    return;
  }
  const P = { l: 46, r: 10, t: 8, b: 22 };
  let x0 = Math.min(...pts.map((p) => p[0])), x1 = Math.max(...pts.map((p) => p[0]));
  let y0 = Math.min(...pts.map((p) => p[1])), y1 = Math.max(...pts.map((p) => p[1]));
  if (limite !== null) { y0 = Math.min(y0, limite); y1 = Math.max(y1, limite); }
  if (x1 === x0) x1 = x0 + 1;
  if (y1 - y0 < 1e-6) { y1 += 1; y0 -= 1; }
  const m = (y1 - y0) * 0.08; y0 -= m; y1 += m;
  const X = (x) => P.l + ((x - x0) / (x1 - x0)) * (w - P.l - P.r);
  const Y = (y) => h - P.b - ((y - y0) / (y1 - y0)) * (h - P.t - P.b);

  g.strokeStyle = "#263142"; g.fillStyle = "#8f9bad"; g.lineWidth = 1;
  g.textAlign = "right"; g.textBaseline = "middle";
  for (let i = 0; i <= 4; i++) {
    const v = y0 + ((y1 - y0) * i) / 4, y = Y(v);
    g.beginPath(); g.moveTo(P.l, y); g.lineTo(w - P.r, y); g.stroke();
    g.fillText(n(v, dec), P.l - 6, y);
  }
  g.textAlign = "center"; g.textBaseline = "top";
  const vistos = new Set();
  for (let i = 0; i <= 4; i++) {
    const v = Math.round(x0 + ((x1 - x0) * i) / 4);
    if (vistos.has(v)) continue;
    vistos.add(v);
    g.textAlign = i === 0 ? "left" : i === 4 ? "right" : "center";
    g.fillText(`día ${v}`, X(v), h - P.b + 6);
  }
  if (limite !== null) {
    g.strokeStyle = "#c9534f"; g.setLineDash([5, 4]);
    g.beginPath(); g.moveTo(P.l, Y(limite)); g.lineTo(w - P.r, Y(limite)); g.stroke(); g.setLineDash([]);
    g.fillStyle = "#ec8a86"; g.textAlign = "right"; g.textBaseline = "bottom";
    g.fillText(`límite ${limite}`, w - P.r - 2, Y(limite) - 2);
  }
  series.forEach((s, i) => {
    if (s.puntos.length < 2) return;
    g.strokeStyle = COLORES[i % COLORES.length]; g.lineWidth = 1.8;
    g.beginPath();
    s.puntos.forEach((p, j) => (j ? g.lineTo(X(p[0]), Y(p[1])) : g.moveTo(X(p[0]), Y(p[1]))));
    g.stroke();
  });
}

function pintarGraficos() {
  const nombres = Object.keys(historia);
  $("leyenda").innerHTML = nombres.map((nm, i) => `<span><i style="background:${COLORES[i % COLORES.length]}"></i>${esc(nm)}</span>`).join("");
  const serie = (m) => nombres.map((nm) => ({ nombre: nm, puntos: historia[nm][m] || [] }));
  const limite = ultimoEstado?.datos?.limite_dosis_msv ?? 600;
  grafico($("g-dosis"), serie("dosis_msv"), { limite, dec: 0 });
  grafico($("g-sans"), serie("sans"), { dec: 2 });
  grafico($("g-hueso"), serie("hueso_pct"), { dec: 1 });
  grafico($("g-musculo"), serie("musculo_pct"), { dec: 1 });
}

// ------------------------------------------------------------------ informes

async function pintarInformes() {
  const lista = await json("/api/informes").catch(() => []);
  $("informes").innerHTML = lista.length ? lista.slice(0, 12).map((r) => `<li><span><b>${esc(r.nombre)}</b> <span class="tenue">· ${esc(r.fecha)} · ${r.kb} KB</span></span>
      <span class="botones"><a class="boton" href="${r.pdf}" target="_blank" rel="noopener">PDF</a>${r.md ? `<a class="boton" href="${r.md}" target="_blank" rel="noopener">Texto</a>` : ""}</span></li>`).join("")
    : `<li class="vacio">Aún no hay informes. Se generan solos con cada evento (tormenta, cambio de fase, alerta crítica) o con el botón “Generar informe PDF”.</li>`;
}

$("btn-pdf").addEventListener("click", async () => {
  const b = $("btn-pdf");
  b.disabled = true; b.textContent = "Generando…";
  try {
    const r = await json("/api/pdf", { method: "POST" });
    if (!r.ok) throw new Error(r.error);
    window.open(r.url, "_blank", "noopener");
    aviso(r.aviso ? `Informe generado (${r.aviso}).` : "Informe PDF generado: se abrió en una pestaña nueva.");
    pintarInformes();
  } catch (err) {
    aviso(`No se pudo generar el informe: ${err.message}`, true);
  } finally {
    b.disabled = false; b.textContent = "Generar informe PDF";
  }
});

// ------------------------------------------------------------------ ciclo

let fallos = 0;
async function cicloEstado() {
  try {
    pintarEstado(await json("/api/estado"));
    fallos = 0;
  } catch {
    if (++fallos === 3) { const p = $("enlace-pill"); p.className = "pill gris"; p.textContent = "○ ESTACIÓN SIN RESPUESTA"; }
  }
  setTimeout(cicloEstado, 1000);
}
async function cicloHistoria() {
  try { historia = await json("/api/historia"); pintarGraficos(); } catch { /* reintenta */ }
  setTimeout(cicloHistoria, 4000);
}
async function cicloInformes() { await pintarInformes(); setTimeout(cicloInformes, 6000); }

window.addEventListener("resize", () => pintarGraficos());
cicloEstado();
cicloHistoria();
cicloInformes();
