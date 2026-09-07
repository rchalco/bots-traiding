# Ejemplo visual del bot EMA 9/21 Retest

## 1) Ejemplo de compra (Buy)

El bot arma una posición larga cuando:

- la tendencia es alcista (EMA 9 > EMA 21, pendiente positiva),
- hubo impulso previo lejos de la EMA 21,
- el precio hace un pullback hacia la banda EMA 9/21,
- y luego aparece una vela de confirmación alcista.

```text
Precio / EMA
      ▲
      │              Cierre alcista
      │            ╱╲
      │          ╱   ╲
      │        ╱      ╲
      │      ╱         ╲
EMA9  ─┼─────●──────────┼──────────
EMA21 ─┼──────●─────────┼──────────
      │      ╲        ╱
      │       ╲      ╱
      │        ╲    ╱
      │         ╲  ╱
      │          ╲╱
      └────────────────────────────► Tiempo
        1) impulso previo   2) pullback   3) confirmación
```

### Secuencia lógica

1. El precio se aleja de la EMA 21 con fuerza (impulso).
2. Luego retrocede hacia la banda EMA 9/21 (pullback).
3. Una vela bullish rompe el máximo anterior y el bot entra en Buy.

---

## 2) Ejemplo de venta (Sell)

El bot arma una posición corta cuando:

- la tendencia es bajista (EMA 9 < EMA 21, pendiente negativa),
- hubo impulso previo por debajo de la EMA 21,
- el precio hace un pullback hacia la banda EMA 9/21,
- y luego aparece una vela de confirmación bajista.

```text
Precio / EMA
      │          ╲
      │           ╲
      │            ╲
      │             ╲
EMA9  ─┼──────────────●─────┼──────────
EMA21 ─┼───────────────●────┼──────────
      │            ╱
      │           ╱
      │          ╱
      │         ╱
      │        ╱
      ▼
      └────────────────────────────► Tiempo
        1) impulso previo   2) pullback   3) confirmación bajista
```

### Secuencia lógica

1. El precio se aleja de la EMA 21 en dirección bajista.
2. Luego retrocede hacia la banda EMA 9/21.
3. Una vela bajista rompe el mínimo anterior y el bot entra en Sell.

---

## 3) Resumen del comportamiento real del bot

- Compra cuando la condición de armado larga se cumple y la vela de confirmación es alcista.
- Venta cuando la condición de armado corta se cumple y la vela de confirmación es bajista.
- Si el setup se invalida por giro de tendencia o por exceso de barras, no entra.
