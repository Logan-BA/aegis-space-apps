//! Protocolo AEGIS de telemetría nave -> Tierra (versión 1). Idéntico al de la simulación (C#) y al
//! receptor de Python.
//!
//! Cadena de la nave:   JSON -> deflate -> AES-256-CBC -> HMAC-SHA256 -> CRC-32 -> Hamming(7,4) + entrelazado
//! Cadena del receptor: desentrelazar + corregir (Hamming) -> CRC-32 -> HMAC -> descifrar -> inflar -> JSON
//!
//! Trama interna (big-endian):
//!   "AEGS"(4) | versión(1) | flags(1) | sesión(4) | secuencia(4) | t_ms(8) | IV(16) | largo(4)
//!   | cifrado(largo) | HMAC(32) | CRC-32(4)

use aes::Aes256;
use cbc::cipher::{block_padding::Pkcs7, BlockDecryptMut, KeyIvInit};
use hmac::{Hmac, Mac};
use sha2::Sha256;
use std::fmt;
use std::io::Read;
use std::sync::OnceLock;

type HmacSha256 = Hmac<Sha256>;
type Aes256CbcDec = cbc::Decryptor<Aes256>;

pub const HEADER_LEN: usize = 42;
pub const MAC_LEN: usize = 32;
pub const DEPTH: usize = 16;

#[derive(Debug)]
pub enum ErrorTrama {
    /// Tan dañada por el ruido que no se pudo reparar.
    Canal(String),
    /// Firma inválida: clave distinta (no autorizado) o trama alterada.
    Autenticacion(String),
    /// Estructura o contenido inválido.
    Formato(String),
}

impl fmt::Display for ErrorTrama {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            ErrorTrama::Canal(m) | ErrorTrama::Autenticacion(m) | ErrorTrama::Formato(m) => write!(f, "{m}"),
        }
    }
}

pub struct Trama {
    pub sesion: u32,
    pub secuencia: u32,
    pub t_ms: u64,
    pub datos: serde_json::Value,
    pub bits_corregidos: usize,
}

// ---------------------------------------------------------------- claves

/// Lee la clave maestra: 64 caracteres hexadecimales (256 bits). Ignora líneas que empiezan con #.
pub fn cargar_clave(ruta: &str) -> Result<[u8; 32], String> {
    let texto = std::fs::read_to_string(ruta).map_err(|e| format!("no se pudo leer '{ruta}': {e}"))?;
    let hexa: String = texto
        .lines()
        .map(str::trim)
        .filter(|l| !l.is_empty() && !l.starts_with('#'))
        .collect();
    if hexa.len() != 64 {
        return Err("la clave debe tener 64 caracteres hexadecimales (256 bits)".into());
    }
    let mut k = [0u8; 32];
    for i in 0..32 {
        k[i] = u8::from_str_radix(&hexa[2 * i..2 * i + 2], 16).map_err(|_| "la clave no es hexadecimal".to_string())?;
    }
    Ok(k)
}

pub fn derivar_claves(maestra: &[u8; 32]) -> ([u8; 32], [u8; 32]) {
    let derivar = |etiqueta: &[u8]| -> [u8; 32] {
        let mut h = <HmacSha256 as Mac>::new_from_slice(maestra).expect("HMAC acepta cualquier largo");
        h.update(etiqueta);
        h.finalize().into_bytes().into()
    };
    (derivar(b"AEGIS-ENC-v1"), derivar(b"AEGIS-MAC-v1"))
}

/// Genera una clave nueva (aleatoria del sistema operativo) en hexadecimal.
pub fn nueva_clave_hex() -> String {
    let mut k = [0u8; 32];
    getrandom::getrandom(&mut k).expect("generador aleatorio del sistema");
    k.iter().map(|b| format!("{b:02x}")).collect()
}

// ---------------------------------------------------------------- Hamming(7,4)

/// Palabra en los bits 0..6: p1 p2 d1 p3 d2 d3 d4 (posiciones 1..7). Devuelve (nibble, ¿corrigió?).
fn decodificar_palabra(c: u8) -> (u8, bool) {
    let mut c = c & 0x7F;
    let bit = |c: u8, i: u8| (c >> i) & 1;
    let s = (bit(c, 0) ^ bit(c, 2) ^ bit(c, 4) ^ bit(c, 6))
        | (bit(c, 1) ^ bit(c, 2) ^ bit(c, 5) ^ bit(c, 6)) << 1
        | (bit(c, 3) ^ bit(c, 4) ^ bit(c, 5) ^ bit(c, 6)) << 2;
    if s != 0 {
        c ^= 1 << (s - 1);
    }
    (bit(c, 2) << 3 | bit(c, 4) << 2 | bit(c, 5) << 1 | bit(c, 6), s != 0)
}

fn tabla() -> &'static [(u8, bool); 256] {
    static T: OnceLock<[(u8, bool); 256]> = OnceLock::new();
    T.get_or_init(|| {
        let mut t = [(0u8, false); 256];
        for (i, v) in t.iter_mut().enumerate() {
            *v = decodificar_palabra(i as u8);
        }
        t
    })
}

/// Desentrelaza y corrige. Devuelve (trama, bits corregidos).
pub fn fec_decodificar(datos: &[u8]) -> Result<(Vec<u8>, usize), ErrorTrama> {
    if datos.is_empty() || datos.len() % DEPTH != 0 {
        return Err(ErrorTrama::Canal("tamaño de bloque inválido".into()));
    }
    let filas = datos.len() / DEPTH;
    let mut cw = vec![0u8; datos.len()];
    for c in 0..DEPTH {
        for r in 0..filas {
            cw[r * DEPTH + c] = datos[c * filas + r];
        }
    }
    let t = tabla();
    let mut salida = Vec::with_capacity(cw.len() / 2);
    let mut corregidos = 0usize;
    for par in cw.chunks_exact(2) {
        let (a, ca) = t[par[0] as usize];
        let (b, cb) = t[par[1] as usize];
        corregidos += ca as usize + cb as usize;
        salida.push(a << 4 | b);
    }
    if salida.len() < 4 {
        return Err(ErrorTrama::Canal("bloque vacío".into()));
    }
    let largo = u32::from_be_bytes([salida[0], salida[1], salida[2], salida[3]]) as usize;
    if largo > salida.len() - 4 {
        return Err(ErrorTrama::Canal("largo dañado por el ruido".into()));
    }
    Ok((salida[4..4 + largo].to_vec(), corregidos))
}

// ---------------------------------------------------------------- trama segura

fn be32(b: &[u8]) -> u32 {
    u32::from_be_bytes([b[0], b[1], b[2], b[3]])
}

pub fn abrir(bloque: &[u8], k_cif: &[u8; 32], k_mac: &[u8; 32]) -> Result<Trama, ErrorTrama> {
    let (t, corregidos) = fec_decodificar(bloque)?;
    if t.len() < HEADER_LEN + 16 + MAC_LEN + 4 {
        return Err(ErrorTrama::Canal("trama demasiado corta".into()));
    }
    let n = t.len();
    if crc32fast::hash(&t[..n - 4]) != be32(&t[n - 4..]) {
        return Err(ErrorTrama::Canal("CRC no coincide: errores que el código no pudo corregir".into()));
    }
    if &t[0..4] != b"AEGS" || t[4] != 1 {
        return Err(ErrorTrama::Formato("no es una trama AEGIS v1".into()));
    }
    let flags = t[5];
    let sesion = be32(&t[6..10]);
    let secuencia = be32(&t[10..14]);
    let t_ms = u64::from_be_bytes(t[14..22].try_into().unwrap());
    let iv = &t[22..38];
    let largo = be32(&t[38..42]) as usize;
    let fin = HEADER_LEN + largo;
    if fin + MAC_LEN + 4 != n {
        return Err(ErrorTrama::Formato("largo del contenido inválido".into()));
    }
    let mut mac = <HmacSha256 as Mac>::new_from_slice(k_mac).expect("HMAC");
    mac.update(&t[..fin]);
    mac.verify_slice(&t[fin..fin + MAC_LEN])
        .map_err(|_| ErrorTrama::Autenticacion("firma inválida: clave incorrecta o trama alterada".into()))?;

    let mut buf = t[HEADER_LEN..fin].to_vec();
    let plano = Aes256CbcDec::new_from_slices(k_cif, iv)
        .map_err(|_| ErrorTrama::Formato("clave o IV inválidos".into()))?
        .decrypt_padded_mut::<Pkcs7>(&mut buf)
        .map_err(|_| ErrorTrama::Formato("relleno AES inválido".into()))?
        .to_vec();
    let plano = if flags & 1 != 0 {
        let mut v = Vec::new();
        flate2::read::DeflateDecoder::new(&plano[..])
            .read_to_end(&mut v)
            .map_err(|e| ErrorTrama::Formato(format!("deflate inválido: {e}")))?;
        v
    } else {
        plano
    };
    let datos = serde_json::from_slice(&plano).map_err(|e| ErrorTrama::Formato(format!("JSON inválido: {e}")))?;
    Ok(Trama { sesion, secuencia, t_ms, datos, bits_corregidos: corregidos })
}
