#!/usr/bin/env bash
# Prepara el proyecto después de clonarlo (Linux / macOS):
#   1. Descarga los modelos de IA que no caben en GitHub (voz de ASTRA y reconocimiento de voz).
#   2. Crea la clave secreta del enlace espacio-Tierra y la copia a la simulación y a las estaciones.
# Nota: la simulación compilada es para Windows; en Linux se usan las estaciones terrenas (Python / Rust).
set -euo pipefail
RAIZ="$(cd "$(dirname "$0")" && pwd)"
SA="$RAIZ/simulacion_unity/Assets/StreamingAssets"

descargar() {  # url destino nombre
  if [ -f "$2" ]; then echo "Ya existe: $3"; return; fi
  echo "Descargando $3..."
  mkdir -p "$(dirname "$2")"
  curl -L --fail -o "$2.parcial" "$1"
  mv "$2.parcial" "$2"
}
descargar "https://huggingface.co/rhasspy/piper-voices/resolve/main/es/es_AR/daniela/high/es_AR-daniela-high.onnx" \
          "$SA/piper/es_AR-daniela-high.onnx" "voz de ASTRA (~109 MB)"
descargar "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small-q5_1.bin" \
          "$SA/whisper/ggml-small-q5_1.bin" "reconocimiento de voz (~181 MB)"

CLAVE="$SA/enlace/clave_nasa.key"
if [ ! -f "$CLAVE" ]; then
  mkdir -p "$(dirname "$CLAVE")"
  HEX="$(python3 -c 'import secrets; print(secrets.token_hex(32))' 2>/dev/null || openssl rand -hex 32)"
  printf '# Clave maestra AEGIS (AES-256 + HMAC-SHA256). SECRETA: solo para estaciones autorizadas.\n%s\n' "$HEX" > "$CLAVE"
  echo "Clave nueva creada."
fi
cp "$CLAVE" "$RAIZ/receptor_python/clave_nasa.key"
cp "$CLAVE" "$RAIZ/receptor_rust/clave_nasa.key"
echo "Clave copiada a la simulación y a receptor_python y receptor_rust."
