//! Estación terrena AEGIS (Rust): recibe por WebSocket la telemetría cifrada que transmite la simulación,
//! corrige el ruido del canal, verifica la firma, descifra, filtra picos de sensores y genera informes.
//!
//!   cargo run --release                                   (escucha en 0.0.0.0:8766, clave clave_nasa.key)
//!   cargo run --release -- --puerto 9000 --cada-dias 10 --nombre "Estación Canberra"
//!   cargo run --release -- --generar-clave clave_nasa.key

mod estacion;
mod filtros;
mod informe;
mod protocolo;

use estacion::Estacion;
use futures_util::StreamExt;
use std::net::UdpSocket;
use std::path::PathBuf;
use std::sync::{Arc, Mutex};
use tokio::net::TcpListener;
use tokio_tungstenite::tungstenite::Message;

const VERDE: &str = "\x1b[32m";
const AMARILLO: &str = "\x1b[33m";
const ROJO: &str = "\x1b[31m";
const GRIS: &str = "\x1b[90m";
const NEGRITA: &str = "\x1b[1m";
const FIN: &str = "\x1b[0m";

struct Opciones {
    host: String,
    puerto: u16,
    clave: String,
    informes: String,
    cada_dias: i64,
    nombre: String,
}

fn opciones() -> Result<Opciones, String> {
    let mut o = Opciones {
        host: "0.0.0.0".into(),
        puerto: 8766,
        clave: "clave_nasa.key".into(),
        informes: "informes".into(),
        cada_dias: 30,
        nombre: "Estación terrena Rust".into(),
    };
    let args: Vec<String> = std::env::args().skip(1).collect();
    let mut i = 0;
    while i < args.len() {
        let valor = |i: usize| args.get(i + 1).cloned().ok_or(format!("falta el valor de {}", args[i]));
        match args[i].as_str() {
            "--host" => o.host = valor(i)?,
            "--puerto" => o.puerto = valor(i)?.parse().map_err(|_| "puerto inválido")?,
            "--clave" => o.clave = valor(i)?,
            "--informes" => o.informes = valor(i)?,
            "--cada-dias" => o.cada_dias = valor(i)?.parse().map_err(|_| "--cada-dias inválido")?,
            "--nombre" => o.nombre = valor(i)?,
            "--generar-clave" => {
                let ruta = args.get(i + 1).cloned().unwrap_or_else(|| "clave_nasa.key".into());
                if std::path::Path::new(&ruta).exists() {
                    return Err(format!("'{ruta}' ya existe; bórrela primero si de verdad quiere reemplazarla"));
                }
                let texto = format!(
                    "# Clave maestra AEGIS (AES-256 + HMAC-SHA256). SECRETA: solo para estaciones autorizadas.\n{}\n",
                    protocolo::nueva_clave_hex()
                );
                std::fs::write(&ruta, texto).map_err(|e| e.to_string())?;
                println!("Clave creada: {ruta}");
                std::process::exit(0);
            }
            "-h" | "--help" => {
                println!("Uso: receptor [--host 0.0.0.0] [--puerto 8766] [--clave clave_nasa.key] [--informes informes] [--cada-dias 30] [--nombre TEXTO] | --generar-clave [ruta]");
                std::process::exit(0);
            }
            otro => return Err(format!("opción desconocida: {otro}")),
        }
        i += 2;
    }
    Ok(o)
}

fn ip_local() -> Option<String> {
    let s = UdpSocket::bind("0.0.0.0:0").ok()?;
    s.connect("10.255.255.255:1").ok()?; // no envía nada: solo elige la interfaz de salida
    let ip = s.local_addr().ok()?.ip().to_string();
    if ip.starts_with("127.") || ip == "0.0.0.0" { None } else { Some(ip) }
}

fn linea(t: &str) {
    println!("\r\x1b[K{t}");
}

fn estado(est: &Estacion) {
    let s = &est.stats;
    let (dia, fase) = est
        .ultimo
        .as_ref()
        .map(|d| (estacion::dia_de(d).to_string(), d["fase"].as_str().unwrap_or("-").to_string()))
        .unwrap_or(("-".into(), "-".into()));
    print!(
        "\r\x1b[K{GRIS}[{}]{FIN} día {dia} · {fase} · válidas {}/{} · bits corregidos {} · descartadas {} · perdidas {} · picos filtrados {} · calidad {:.0} %",
        &estacion::utc_ahora(true)[11..19], s.validas, s.recibidas, s.bits_corregidos, s.descartadas_ruido,
        s.perdidas_canal(), s.picos_filtrados, s.calidad()
    );
    use std::io::Write;
    let _ = std::io::stdout().flush();
}

#[tokio::main]
async fn main() {
    let _ = enable_ansi_support::enable_ansi_support();
    let o = match opciones() {
        Ok(o) => o,
        Err(e) => {
            eprintln!("{e}");
            std::process::exit(2);
        }
    };
    let maestra = match protocolo::cargar_clave(&o.clave) {
        Ok(k) => k,
        Err(e) => {
            eprintln!("No se pudo cargar la clave ({e}). Cópiela de la simulación o genere una con: cargo run --release -- --generar-clave");
            std::process::exit(1);
        }
    };
    let (k_cif, k_mac) = protocolo::derivar_claves(&maestra);
    let est = Arc::new(Mutex::new(Estacion::new(k_cif, k_mac, PathBuf::from(&o.informes), o.cada_dias)));
    let nombre = Arc::new(o.nombre.clone());

    let listener = match TcpListener::bind((o.host.as_str(), o.puerto)).await {
        Ok(l) => l,
        Err(e) => {
            eprintln!("No se pudo escuchar en {}:{} ({e})", o.host, o.puerto);
            std::process::exit(1);
        }
    };
    println!("{NEGRITA}Estación terrena AEGIS · {}{FIN}", o.nombre);
    println!("Escuchando en ws://{}:{}  (clave: {})", o.host, o.puerto, o.clave);
    if let Some(ip) = ip_local() {
        println!("  Desde otra computadora, la simulación debe enviar a:  ws://{ip}:{}", o.puerto);
    }
    println!("Informes en: {}   ·   Ctrl+C para terminar\n", o.informes);

    let aceptar = async {
        loop {
            let Ok((tcp, origen)) = listener.accept().await else { continue };
            let est = est.clone();
            let nombre = nombre.clone();
            tokio::spawn(async move {
                let Ok(ws) = tokio_tungstenite::accept_async(tcp).await else { return };
                linea(&format!("{VERDE}● Nave conectada desde {origen}{FIN}"));
                let (_escritura, mut lectura) = ws.split();
                while let Some(msg) = lectura.next().await {
                    let datos = match msg {
                        Ok(Message::Binary(b)) => b,
                        Ok(Message::Close(_)) | Err(_) => break,
                        _ => continue,
                    };
                    let mut e = est.lock().unwrap();
                    let r = e.recibir(&datos);
                    if !r.ok {
                        let color = if r.mensaje.contains("RECHAZADA") { ROJO } else { AMARILLO };
                        linea(&format!("{color}✗ {}{FIN}", r.mensaje));
                    } else if let Some(motivo) = r.motivo_informe {
                        let (titular, ruta) = informe::generar(&mut e, &motivo, &nombre);
                        let general = e.ultimo.as_ref().map(informe::estado_general).unwrap_or("NOMINAL");
                        let color = match general { "CRÍTICO" => ROJO, "PRECAUCIÓN" => AMARILLO, _ => VERDE };
                        linea(&format!("{NEGRITA}{color}■ INFORME ({motivo}){FIN} {titular}\n  {GRIS}→ {ruta}{FIN}"));
                    }
                    estado(&e);
                }
                linea(&format!("{AMARILLO}○ Nave desconectada ({origen}){FIN}"));
            });
        }
    };

    tokio::select! {
        _ = aceptar => {}
        _ = tokio::signal::ctrl_c() => {}
    }
    let mut e = est.lock().unwrap();
    if e.ultimo.is_some() {
        let (titular, ruta) = informe::generar(&mut e, "cierre de la estación", &nombre);
        linea(&format!("{NEGRITA}■ INFORME FINAL{FIN} {titular}\n  → {ruta}"));
    }
    println!("Estación detenida.");
}
