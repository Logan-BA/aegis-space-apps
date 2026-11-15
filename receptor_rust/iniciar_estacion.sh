#!/usr/bin/env bash
# Estacion terrena AEGIS (Rust). Compila la primera vez y arranca.
set -e
cd "$(dirname "$0")"
[ -x target/release/receptor ] || cargo build --release
exec ./target/release/receptor "$@"
