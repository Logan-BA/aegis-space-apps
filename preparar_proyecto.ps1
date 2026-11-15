# Prepara el proyecto después de clonarlo desde GitHub (Windows / PowerShell):
#   1. Descarga los modelos de IA que no caben en GitHub (voz de ASTRA y reconocimiento de voz).
#   2. Crea la clave secreta del enlace espacio-Tierra y la copia a la simulación y a las dos estaciones.
# Uso:  powershell -ExecutionPolicy Bypass -File preparar_proyecto.ps1
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$raiz = Split-Path -Parent $MyInvocation.MyCommand.Path
$sa = Join-Path $raiz "simulacion_unity\Assets\StreamingAssets"
$app = Join-Path $raiz "App\SaludAstronauta\SaludAstronauta_Data\StreamingAssets"

$modelos = @(
    @{ Url = "https://huggingface.co/rhasspy/piper-voices/resolve/main/es/es_AR/daniela/high/es_AR-daniela-high.onnx"; Ruta = "piper\es_AR-daniela-high.onnx"; MB = 109; Nombre = "voz de ASTRA (Piper es_AR daniela)" },
    @{ Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small-q5_1.bin"; Ruta = "whisper\ggml-small-q5_1.bin"; MB = 181; Nombre = "reconocimiento de voz (Whisper small)" }
)
foreach ($m in $modelos) {
    $destino = Join-Path $sa $m.Ruta
    if (Test-Path $destino) { Write-Host "Ya existe: $($m.Nombre)"; continue }
    Write-Host "Descargando $($m.Nombre) (~$($m.MB) MB)..."
    New-Item -ItemType Directory -Force (Split-Path $destino) | Out-Null
    Invoke-WebRequest -Uri $m.Url -OutFile "$destino.parcial" -UseBasicParsing
    Move-Item "$destino.parcial" $destino -Force
}

# Clave del enlace (256 bits). Nunca se sube a GitHub.
$clave = Join-Path $sa "enlace\clave_nasa.key"
if (-not (Test-Path $clave)) {
    $bytes = New-Object byte[] 32
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $hex = -join ($bytes | ForEach-Object { $_.ToString("x2") })
    New-Item -ItemType Directory -Force (Split-Path $clave) | Out-Null
    $texto = "# Clave maestra AEGIS (AES-256 + HMAC-SHA256). SECRETA: solo para estaciones autorizadas.`n$hex`n"
    [IO.File]::WriteAllText($clave, $texto, (New-Object Text.UTF8Encoding($false)))
    Write-Host "Clave nueva creada."
}
foreach ($d in @("receptor_python", "receptor_rust")) { Copy-Item $clave (Join-Path $raiz "$d\clave_nasa.key") -Force }
if (Test-Path $app) {
    New-Item -ItemType Directory -Force (Join-Path $app "enlace") | Out-Null
    Copy-Item $clave (Join-Path $app "enlace\clave_nasa.key") -Force
}
Write-Host "Clave copiada a la simulación y a receptor_python y receptor_rust."

Write-Host ""
Write-Host "Listo. Para que ASTRA use la IA local instale Ollama (https://ollama.com) y ejecute:"
Write-Host "  ollama pull qwen2.5:3b"
Write-Host "  powershell -ExecutionPolicy Bypass -File IA\entrenar_astra.ps1"
