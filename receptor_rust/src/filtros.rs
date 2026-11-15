//! Filtros de la estación terrena.
//!
//! El ruido digital (bits invertidos) ya lo resuelven Hamming + CRC en `protocolo.rs`. Aquí se trata el
//! ruido que llega DENTRO de datos válidos: un impacto de radiación en la electrónica de un sensor (SEU)
//! puede producir una lectura absurda. Se usan límites de plausibilidad del sensor y el filtro de Hampel
//! (mediana + desviación absoluta mediana), que quita picos sin suavizar la tendencia.

use std::collections::VecDeque;

/// (variable, mínimo plausible, máximo plausible, piso del umbral de Hampel)
pub const SENSORES: [(&str, f64, f64, f64); 5] = [
    ("fc", 30.0, 200.0, 10.0),
    ("pas", 70.0, 200.0, 15.0),
    ("pad", 40.0, 130.0, 12.0),
    ("spo2", 75.0, 100.0, 3.0),
    ("temp_c", 34.0, 41.0, 0.8),
];

pub fn mediana(v: &[f64]) -> f64 {
    let mut s = v.to_vec();
    s.sort_by(|a, b| a.partial_cmp(b).unwrap_or(std::cmp::Ordering::Equal));
    let m = s.len() / 2;
    if s.len() % 2 == 0 { (s[m - 1] + s[m]) / 2.0 } else { s[m] }
}

pub struct FiltroHampel {
    min: f64,
    max: f64,
    piso: f64,
    k: f64,
    ventana: usize,
    valores: VecDeque<f64>,
}

impl FiltroHampel {
    pub fn new(min: f64, max: f64, piso: f64) -> Self {
        Self { min, max, piso, k: 3.5, ventana: 9, valores: VecDeque::new() }
    }

    /// Devuelve (valor aceptado, motivo del descarte si lo hubo).
    pub fn filtrar(&mut self, x: f64) -> (f64, Option<String>) {
        let v: Vec<f64> = self.valores.iter().copied().collect();
        let referencia = if v.is_empty() { None } else { Some(mediana(&v)) };
        if x < self.min || x > self.max {
            return (referencia.unwrap_or(x), Some("fuera del rango físico del sensor".into()));
        }
        if let Some(r) = referencia {
            if v.len() >= 5 {
                let desv: Vec<f64> = v.iter().map(|a| (a - r).abs()).collect();
                let mad = mediana(&desv) * 1.4826;
                if (x - r).abs() > (self.k * mad).max(self.piso) {
                    return (r, Some(format!("pico aislado; mediana reciente {}", crate::informe::n(r, 1))));
                }
            }
        }
        self.valores.push_back(x);
        if self.valores.len() > self.ventana {
            self.valores.pop_front();
        }
        (x, None)
    }
}

/// Pendiente por mínimos cuadrados (unidades por día). None si no hay datos suficientes.
pub fn pendiente(puntos: &[(f64, f64)]) -> Option<f64> {
    if puntos.len() < 3 {
        return None;
    }
    let n = puntos.len() as f64;
    let mx = puntos.iter().map(|p| p.0).sum::<f64>() / n;
    let my = puntos.iter().map(|p| p.1).sum::<f64>() / n;
    let sxx: f64 = puntos.iter().map(|p| (p.0 - mx).powi(2)).sum();
    if sxx == 0.0 {
        return None;
    }
    Some(puntos.iter().map(|p| (p.0 - mx) * (p.1 - my)).sum::<f64>() / sxx)
}
