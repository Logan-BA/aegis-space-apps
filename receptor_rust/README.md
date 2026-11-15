# Estación terrena AEGIS · Rust

Misma función que la estación de Python, compilada en un ejecutable nativo y rápido: recibe por **WebSocket**
la telemetría cifrada de la nave, corrige el ruido del canal, verifica la firma, descifra, filtra picos de los
sensores y escribe **informes**. Protocolo y justificación: [../ENLACE_ESPACIO_TIERRA.md](../ENLACE_ESPACIO_TIERRA.md).

## Archivos

| Archivo | Qué hace |
| --- | --- |
| `src/main.rs` | Servidor WebSocket (puerto **8766**), opciones, consola en vivo. |
| `src/protocolo.rs` | Hamming(7,4) + entrelazado, CRC-32, HMAC-SHA256, AES-256-CBC, deflate; generar clave. |
| `src/filtros.rs` | Filtro de Hampel, límites de plausibilidad, tendencias. |
| `src/estacion.rs` | Estadísticas del enlace, historia por tripulante, CSV. |
| `src/informe.rs` | Informe en Markdown. |
| `Cargo.toml` | Librerías (crates): `tokio`, `tokio-tungstenite`, `futures-util`, `serde_json`, `aes`, `cbc`, `hmac`, `sha2`, `crc32fast`, `flate2`, `getrandom`, `enable-ansi-support`. |
| `clave_nasa.key` | Clave de la misión (la misma que usa la simulación). **Secreta.** |

Las librerías se descargan solas al compilar (`cargo build`); no hay que instalarlas a mano.

## Instalación de Rust

### Windows
Rust necesita las herramientas de compilación de C++ de Microsoft (MSVC):
```bash
winget install -e --id Microsoft.VisualStudio.2022.BuildTools --override "--quiet --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended"
```
```bash
winget install -e --id Rustlang.Rustup
```
Cierre y vuelva a abrir la terminal, y verifique:
```bash
cargo --version
```

### Linux (Ubuntu / Debian)
```bash
sudo apt update && sudo apt install -y build-essential curl
```
```bash
curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y
```
```bash
source "$HOME/.cargo/env"
```
(Fedora: `sudo dnf install -y gcc curl`; Arch: `sudo pacman -S base-devel rustup && rustup default stable`.)

## Compilar y ejecutar
```bash
cd receptor_rust
```
```bash
cargo build --release
```
Windows:
```bash
target\release\receptor.exe
```
Linux:
```bash
./target/release/receptor
```
O en un solo paso: `cargo run --release -- --cada-dias 10`.

Atajos: `iniciar_estacion.bat` (Windows) e `iniciar_estacion.sh` (Linux) compilan si hace falta y arrancan.

Opciones:

| Opción | Por defecto | Descripción |
| --- | --- | --- |
| `--puerto` | `8766` | Puerto WebSocket |
| `--host` | `0.0.0.0` | Interfaz (0.0.0.0 = acepta conexiones de otras computadoras) |
| `--clave` | `clave_nasa.key` | Archivo de la clave |
| `--informes` | `informes` | Carpeta de salida |
| `--cada-dias` | `30` | Informe periódico cada N días de misión (0 = solo eventos) |
| `--nombre` | `Estación terrena Rust` | Nombre en los informes |
| `--generar-clave [ruta]` | — | Crea una clave nueva de 256 bits y sale |

En la simulación agregue `ws://IP_DE_ESTA_PC:8766` a `estaciones` en `StreamingAssets/enlace/enlace.json`
(o `SaludAstronauta.exe -estacion ws://IP:8766`).

### Probar sin la simulación
Con la estación Rust corriendo, use el emisor de la carpeta de Python:
```bash
cd ../receptor_python
.venv\Scripts\python emisor_prueba.py --url ws://127.0.0.1:8766
```

Seguridad: no publique `clave_nasa.key` (está en `.gitignore`).
