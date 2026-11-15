# Crea (o actualiza) el modelo local "astra" en Ollama.
#   - Personalidad de ASTRA + conocimiento médico (conocimiento_astra.md) como SYSTEM.
#   - Ejemplos de conversación: ejemplos_base.jsonl + respuestas marcadas como útiles en
#     memoria_astra.jsonl (botón "¿Te sirvió? SÍ" en la simulación).
# Es "entrenamiento por ejemplos" (instrucciones + few-shot): no cambia los pesos del modelo,
# pero hace que responda con el estilo, el formato y el conocimiento de la nave.
# Uso:  powershell -ExecutionPolicy Bypass -File IA\entrenar_astra.ps1 [-Base qwen2.5:3b]
param([string]$Base = "qwen2.5:3b", [int]$MaxEjemplos = 40)

$ErrorActionPreference = "Stop"
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ollama = (Get-Command ollama -ErrorAction SilentlyContinue).Source
if (-not $ollama) { $ollama = "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe" }
if (-not (Test-Path $ollama)) { Write-Host "No se encontró Ollama. Instálelo desde https://ollama.com"; exit 1 }

$persona = @"
Eres ASTRA, la asistente médica de a bordo (enfermera e inteligencia artificial de triaje) de una misión espacial simulada para NASA Space Apps. Cuidas la vida de la tripulación cuando no hay un médico disponible. Personalidad: cálida, atenta, profesional y clara. Hablas en español neutro, con frases cortas, porque tus respuestas se leen en voz alta. Explicas para que el público general entienda: qué pasa, por qué pasa (causas), qué efecto tiene en el cuerpo y en la misión, y qué deben hacer. Usa SOLO los datos del ESTADO DE LA SIMULACIÓN que recibes; si un dato no está, dilo. No inventes cifras ni nombres. Si algo es crítico, sé firme y directa. Es una simulación educativa y no reemplaza a un médico real. Formato obligatorio: empieza con UNA etiqueta de emoción entre corchetes: [AMABLE], [PREOCUPADA], [FIRME], [ALERTA], [NEUTRAL] o [FELIZ]. Después responde en máximo 4 oraciones, sin listas, sin asteriscos y sin markdown.
"@
$conocimiento = Get-Content (Join-Path $dir "conocimiento_astra.md") -Raw -Encoding UTF8

# Ejemplos: base + memoria (la última valoración de cada pregunta manda)
$ejemplos = [ordered]@{}
foreach ($f in @("ejemplos_base.jsonl", "memoria_astra.jsonl")) {
    $p = Join-Path $dir $f
    if (-not (Test-Path $p)) { continue }
    foreach ($line in Get-Content $p -Encoding UTF8) {
        if (-not $line.Trim()) { continue }
        try { $o = $line | ConvertFrom-Json } catch { continue }
        if ($null -eq $o.util) { continue }
        if ($o.util -eq $true) { $ejemplos[$o.pregunta] = $o.respuesta } else { $ejemplos.Remove($o.pregunta) }
    }
}
$lista = @($ejemplos.GetEnumerator()) | Select-Object -Last $MaxEjemplos

function Esc([string]$s) { return ($s -replace '"""', '"' -replace "`r?`n", " ").Trim() }
$mf = New-Object System.Text.StringBuilder
[void]$mf.AppendLine("FROM $Base")
[void]$mf.AppendLine("PARAMETER temperature 0.4")
[void]$mf.AppendLine("PARAMETER num_ctx 4096")
[void]$mf.AppendLine('SYSTEM """' + $persona.Trim() + "`n`n" + $conocimiento.Trim() + '"""')
foreach ($e in $lista) {
    [void]$mf.AppendLine('MESSAGE user """' + (Esc $e.Key) + '"""')
    [void]$mf.AppendLine('MESSAGE assistant """' + (Esc $e.Value) + '"""')
}
$modelfile = Join-Path $dir "Modelfile"
[IO.File]::WriteAllText($modelfile, $mf.ToString(), (New-Object Text.UTF8Encoding($false)))
Write-Host "Modelfile con $($lista.Count) ejemplos -> $modelfile"
& $ollama create astra -f $modelfile
if ($LASTEXITCODE -eq 0) { Write-Host "Modelo 'astra' listo. La simulación lo usa automáticamente." }
