"""Protocolo AEGIS de telemetría nave -> Tierra (versión 1).

Cadena de la nave:  JSON -> deflate -> AES-256-CBC -> HMAC-SHA256 -> CRC-32 -> Hamming(7,4) + entrelazado -> canal
Cadena del receptor (este módulo hace el camino inverso):
    1. Desentrelazar y corregir con Hamming(7,4)  -> repara 1 bit por palabra (ruido del espacio)
    2. CRC-32                                      -> si aún hay errores, la trama se descarta
    3. HMAC-SHA256                                 -> solo acepta tramas firmadas con la clave de la misión
    4. AES-256-CBC + deflate                       -> descifra y descomprime el JSON

Trama interna (big-endian):
    "AEGS"(4) | versión(1) | flags(1) | sesión(4) | secuencia(4) | t_ms(8) | IV(16) | largo(4)
    | cifrado(largo) | HMAC(32) | CRC-32(4)
"""
from __future__ import annotations

import hashlib
import hmac
import json
import os
import struct
import zlib
from dataclasses import dataclass

from cryptography.hazmat.primitives import padding
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes

MAGIC = b"AEGS"
VERSION = 1
HEADER = struct.Struct(">4sBBIIQ16sI")  # 42 bytes
MAC_LEN = 32
DEPTH = 16  # profundidad del entrelazado


class ErrorCanal(Exception):
    """La trama llegó tan dañada por el ruido que no se pudo reparar."""


class ErrorAutenticacion(Exception):
    """La firma no coincide: clave distinta (no autorizado) o trama alterada."""


class ErrorFormato(Exception):
    """Estructura o contenido inválido."""


# ---------------------------------------------------------------- claves

def cargar_clave(ruta: str) -> bytes:
    """Lee la clave maestra: 64 caracteres hexadecimales (256 bits). Ignora líneas que empiezan con #."""
    with open(ruta, encoding="utf-8") as f:
        hexa = "".join(l.strip() for l in f if l.strip() and not l.strip().startswith("#"))
    if len(hexa) != 64:
        raise ValueError("La clave debe tener 64 caracteres hexadecimales (256 bits)")
    return bytes.fromhex(hexa)


def derivar_claves(maestra: bytes) -> tuple[bytes, bytes]:
    k_cif = hmac.new(maestra, b"AEGIS-ENC-v1", hashlib.sha256).digest()
    k_mac = hmac.new(maestra, b"AEGIS-MAC-v1", hashlib.sha256).digest()
    return k_cif, k_mac


# ---------------------------------------------------------------- Hamming(7,4)

def hamming_codificar(nibble: int) -> int:
    """Palabra de 7 bits: p1 p2 d1 p3 d2 d3 d4 (bits 0..6)."""
    d1, d2, d3, d4 = (nibble >> 3) & 1, (nibble >> 2) & 1, (nibble >> 1) & 1, nibble & 1
    p1, p2, p3 = d1 ^ d2 ^ d4, d1 ^ d3 ^ d4, d2 ^ d3 ^ d4
    return p1 | p2 << 1 | d1 << 2 | p3 << 3 | d2 << 4 | d3 << 5 | d4 << 6


def _decodificar_palabra(c: int) -> tuple[int, bool]:
    b = [(c >> i) & 1 for i in range(7)]  # b[0] = posición 1
    s = (b[0] ^ b[2] ^ b[4] ^ b[6]) | (b[1] ^ b[2] ^ b[5] ^ b[6]) << 1 | (b[3] ^ b[4] ^ b[5] ^ b[6]) << 2
    if s:
        c ^= 1 << (s - 1)
        b = [(c >> i) & 1 for i in range(7)]
    return (b[2] << 3 | b[4] << 2 | b[5] << 1 | b[6]), s != 0


# Tablas de 256 entradas (el bit 7 no se usa) para decodificar rápido con bytes.translate
_NIBBLE = bytes(_decodificar_palabra(i & 0x7F)[0] for i in range(256))
_CORRIGIO = bytes(1 if _decodificar_palabra(i & 0x7F)[1] else 0 for i in range(256))


def fec_codificar(trama: bytes) -> bytes:
    datos = struct.pack(">I", len(trama)) + trama
    cw = bytearray()
    for byte in datos:
        cw.append(hamming_codificar(byte >> 4))
        cw.append(hamming_codificar(byte & 0xF))
    filas = -(-len(cw) // DEPTH)
    cw.extend(b"\x00" * (filas * DEPTH - len(cw)))
    salida = bytearray(len(cw))
    for c in range(DEPTH):
        salida[c * filas:(c + 1) * filas] = cw[c::DEPTH]
    return bytes(salida)


def fec_decodificar(datos: bytes) -> tuple[bytes, int]:
    """Desentrelaza y corrige. Devuelve (trama, bits corregidos)."""
    if not datos or len(datos) % DEPTH:
        raise ErrorCanal("tamaño de bloque inválido")
    filas = len(datos) // DEPTH
    cw = bytearray(len(datos))
    for c in range(DEPTH):
        cw[c::DEPTH] = datos[c * filas:(c + 1) * filas]
    nib = cw.translate(_NIBBLE)
    corregidos = len(cw) - cw.translate(_CORRIGIO).count(0)
    salida = bytes((a << 4) | b for a, b in zip(nib[0::2], nib[1::2]))
    largo = struct.unpack(">I", salida[:4])[0]
    if largo > len(salida) - 4:
        raise ErrorCanal("largo dañado por el ruido")
    return salida[4:4 + largo], corregidos


# ---------------------------------------------------------------- trama segura

@dataclass
class Trama:
    sesion: int
    secuencia: int
    t_ms: int
    datos: dict
    bits_corregidos: int


def abrir(bloque: bytes, k_cif: bytes, k_mac: bytes) -> Trama:
    trama, corregidos = fec_decodificar(bloque)
    if len(trama) < HEADER.size + 16 + MAC_LEN + 4:
        raise ErrorCanal("trama demasiado corta")
    crc = struct.unpack(">I", trama[-4:])[0]
    if zlib.crc32(trama[:-4]) & 0xFFFFFFFF != crc:
        raise ErrorCanal("CRC no coincide: errores que el código no pudo corregir")
    magia, version, flags, sesion, seq, t_ms, iv, largo = HEADER.unpack_from(trama)
    if magia != MAGIC or version != VERSION:
        raise ErrorFormato("no es una trama AEGIS v1")
    fin = HEADER.size + largo
    if fin + MAC_LEN + 4 != len(trama):
        raise ErrorFormato("largo del contenido inválido")
    firma = hmac.new(k_mac, trama[:fin], hashlib.sha256).digest()
    if not hmac.compare_digest(firma, trama[fin:fin + MAC_LEN]):
        raise ErrorAutenticacion("firma inválida: clave incorrecta o trama alterada")
    dec = Cipher(algorithms.AES(k_cif), modes.CBC(iv)).decryptor()
    plano = dec.update(trama[HEADER.size:fin]) + dec.finalize()
    quitar = padding.PKCS7(128).unpadder()
    plano = quitar.update(plano) + quitar.finalize()
    if flags & 1:
        plano = zlib.decompress(plano, -15)  # deflate "crudo"
    try:
        datos = json.loads(plano.decode("utf-8"))
    except Exception as e:  # noqa: BLE001
        raise ErrorFormato(f"JSON inválido: {e}") from e
    return Trama(sesion, seq, t_ms, datos, corregidos)


def sellar(datos: dict, k_cif: bytes, k_mac: bytes, sesion: int, seq: int, t_ms: int) -> bytes:
    """Camino de la nave (lo usa emisor_prueba.py): JSON -> trama cifrada -> bloque FEC."""
    plano = zlib.compress(json.dumps(datos, ensure_ascii=False).encode("utf-8"), 9)[2:-4]  # deflate crudo
    iv = os.urandom(16)
    rellenar = padding.PKCS7(128).padder()
    plano = rellenar.update(plano) + rellenar.finalize()
    enc = Cipher(algorithms.AES(k_cif), modes.CBC(iv)).encryptor()
    cifrado = enc.update(plano) + enc.finalize()
    cab = HEADER.pack(MAGIC, VERSION, 1, sesion, seq, t_ms, iv, len(cifrado))
    cuerpo = cab + cifrado
    cuerpo += hmac.new(k_mac, cuerpo, hashlib.sha256).digest()
    cuerpo += struct.pack(">I", zlib.crc32(cuerpo) & 0xFFFFFFFF)
    return fec_codificar(cuerpo)
