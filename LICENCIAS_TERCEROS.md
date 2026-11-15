# Licencias de terceros

## Avatar de ASTRA: Microsoft Rocketbox (Medical_Female_02)
Fuente: https://github.com/microsoft/Microsoft-Rocketbox — Licencia MIT.

MIT License

Copyright (c) 2020 Microsoft

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.


## Voz de ASTRA: Piper TTS + voz es_AR "daniela" (high)
- Piper (motor de voz neuronal local): https://github.com/rhasspy/piper — Licencia MIT.
- Voz es_AR-daniela-high: https://huggingface.co/rhasspy/piper-voices — entrenada con el corpus
  OpenSLR 61 (https://www.openslr.org/61/). Licencia Creative Commons Atribución-CompartirIgual 4.0
  (CC BY-SA 4.0). Atribución: voz "daniela" de Piper / OpenSLR 61.

## Reconocimiento de voz: whisper.cpp + modelo Whisper small (q5_1)
- whisper.cpp: https://github.com/ggml-org/whisper.cpp — Licencia MIT (incluye OpenBLAS, BSD-3; SDL2, zlib).
- Modelo Whisper small de OpenAI (convertido a ggml): https://huggingface.co/ggerganov/whisper.cpp — Licencia MIT.

## IA local: Ollama + Qwen2.5 3B
- Ollama: https://ollama.com — Licencia MIT.
- Qwen2.5-3B-Instruct (Alibaba Cloud): "Qwen Research License" (uso de investigación / no comercial;
  adecuado para el hackatón). Ver https://huggingface.co/Qwen/Qwen2.5-3B-Instruct

## Estaciones terrenas (receptor_python / receptor_rust)
- Python: websockets (BSD-3), cryptography (Apache 2.0 / BSD), reportlab (BSD) para los informes PDF.
- Rust: tokio, tokio-tungstenite, futures-util, serde_json, aes, cbc, hmac, sha2, crc32fast, flate2, getrandom, enable-ansi-support (MIT / Apache 2.0).
