# -*- coding: utf-8 -*-
"""Historias de usuario HU-01 a HU-08 (ITER-1 e inicio de ITER-2)."""
from jira_lib import p, h, bullets


def hu_desc(historia, requisito, criterios, verificacion):
    return [
        h(2, "Historia de usuario"), p(historia),
        h(3, "Requisito (ISO/IEC/IEEE 29148:2018)"), p(requisito),
        h(3, "Criterios de aceptacion"), bullets(criterios),
        h(3, "Verificacion"), p(verificacion),
    ]


ISSUES_B = [
    dict(id="HU-01", type="Story", epic=2, sprint=0, status="done", pts=3,
         prio="Highest", labels=["gameplay", "iter-1"], version=0,
         comments=["Sprint Review ITER-1: aceptada por el cliente. Movimiento fluido verificado en build v0.1.0."],
         summary="Movimiento en primera persona con WASD y mouse",
         desc=hu_desc(
             "Como jugador, quiero moverme en primera persona con WASD y mouse para explorar el nivel libremente.",
             "RF-01: El sistema debera permitir desplazamiento en 8 direcciones con WASD y rotacion de camara con mouse, con sensibilidad configurable, impidiendo atravesar muros o colisiones.",
             ["Movimiento en 8 direcciones sin atravesar muros.",
              "La camara rota 360 grados sin inversiones ni bloqueos.",
              "El jugador no puede salir del area jugable delimitada."],
             "Prueba funcional: recorrido de prueba chocando contra paredes y limites del mapa; 0 atravesamientos en 20 intentos.")),
    dict(id="HU-02", type="Story", epic=2, sprint=0, status="done", pts=5,
         prio="High", labels=["nivel", "iter-1"], version=0, comments=[],
         summary="Escena de habitacion modular con atmosfera oscura",
         desc=hu_desc(
             "Como jugador, quiero explorar una habitacion cerrada con ambiente oscuro para sentir tension desde el inicio.",
             "RF-02: El sistema debera cargar una escena de habitacion construida con piezas modulares, con iluminacion tenue y zonas completamente oscuras.",
             ["La escena carga sin errores en el equipo de referencia.",
              "Existe al menos una zona iluminada y una zona oscura diferenciadas.",
              "Todas las piezas modulares tienen colisiones activas."],
             "Inspeccion visual + prueba de colisiones en el recorrido completo.")),
    dict(id="HU-03", type="Story", epic=2, sprint=0, status="done", pts=3,
         prio="Highest", labels=["gameplay", "iter-1"], version=0, comments=[],
         summary="Interruptor de luz interactivo con tecla E (Raycast)",
         desc=hu_desc(
             "Como jugador, quiero encender la luz apuntando al interruptor y presionando E para revelar la zona oscura.",
             "RF-03: Si el jugador apunta al interruptor a una distancia <= 3 m y presiona E, el sistema debera conmutar el estado de la luz en <= 200 ms.",
             ["La luz solo conmuta con el rayo sobre el interruptor, a 3 m o menos.",
              "El cambio de iluminacion es inmediato y sin parpadeos de renderizado.",
              "Se muestra pista de interaccion cuando el interruptor es apuntado."],
             "Prueba funcional con medicion de distancia y tiempo de respuesta en 10 activaciones.")),
    dict(id="HU-04", type="Story", epic=2, sprint=0, status="done", pts=5,
         prio="Highest", labels=["gameplay", "iter-1"], version=0, comments=[],
         summary="Objeto clave oculto (botiquin) visible solo con luz encendida",
         desc=hu_desc(
             "Como jugador, quiero que el objeto clave solo sea visible con la luz encendida para que la exploracion tenga sentido.",
             "RF-04: El objeto clave debera permanecer no visible y no interactuable mientras la luz de su zona este apagada, y volverse visible al encenderla.",
             ["Con luz apagada, el botiquin no se renderiza ni responde al Raycast.",
              "Con luz encendida, el botiquin es visible e interactuable.",
              "Apagar la luz vuelve a ocultar el botiquin (reversible)."],
             "Prueba de estados: 4 combinaciones luz x visibilidad verificadas una por una.")),
    dict(id="HU-05", type="Story", epic=2, sprint=0, status="done", pts=3,
         prio="High", labels=["gameplay", "UI", "iter-1"], version=0, comments=[],
         summary="Temporizador de nivel con condicion de derrota",
         desc=hu_desc(
             "Como jugador, quiero ver un temporizador en pantalla para sentir presion por escapar a tiempo.",
             "RF-05: El sistema debera mostrar un temporizador regresivo (MM:SS) y, al llegar a 00:00 sin completar el objetivo, mostrar la pantalla de derrota y bloquear el control.",
             ["El reloj se actualiza cada segundo en formato MM:SS.",
              "A 00:00 se muestra la pantalla de derrota y el jugador pierde el control.",
              "El tiempo restante es configurable desde el inspector."],
             "Cronometrar 5 ejecuciones comparando tiempo real vs. mostrado (desviacion <= 1 s).")),
    dict(id="HU-06", type="Story", epic=2, sprint=0, status="done", pts=3,
         prio="High", labels=["gameplay", "UI", "iter-1"], version=0, comments=[],
         summary="Pantalla de victoria al encontrar el objeto clave",
         desc=hu_desc(
             "Como jugador, quiero ver una pantalla de victoria al encontrar el objeto clave para saber que complete el nivel.",
             "RF-06: Al apuntar el objeto clave visible a <= 3 m, el sistema debera declarar la victoria, detener el temporizador y mostrar la pantalla correspondiente en <= 500 ms.",
             ["La victoria solo se dispara con el objeto visible y apuntado.",
              "El temporizador se detiene exactamente al ganar.",
              "La pantalla de victoria ofrece opcion de reiniciar el nivel."],
             "Prueba funcional de transicion a victoria + reinicio del nivel.")),
    dict(id="HU-07", type="Story", epic=2, sprint=1, status="done", pts=5,
         prio="Highest", labels=["terror", "iter-2"], version=1,
         comments=["Sprint Review ITER-2: el cliente valoro el aumento de tension. Se mantiene el umbral por defecto (70%)."],
         summary="Sistema de ansiedad con audio de latido y efectos visuales",
         desc=hu_desc(
             "Como jugador, quiero que mi ansiedad suba en la oscuridad (latido y efectos) para sentir miedo constante.",
             "RF-07: El sistema debera incrementar el nivel de ansiedad del jugador en zonas oscuras a razon de >= 5%/s, reproduciendo audio de latido con intensidad proporcional al nivel (umbrales 40% y 70%).",
             ["La ansiedad sube en oscuridad y baja con luz encendida.",
              "El latido se intensifica al superar el umbral del 70%.",
              "La UI de ansiedad refleja el valor actual en tiempo real."],
             "Prueba instrumentada: registrar nivel de ansiedad y activacion de audio en recorrido controlado de 60 s.")),
    dict(id="HU-08", type="Story", epic=2, sprint=1, status="done", pts=3,
         prio="Medium", labels=["terror", "iluminacion", "iter-2"], version=1, comments=[],
         summary="Luces parpadeantes (flickering) en zonas de tension",
         desc=hu_desc(
             "Como jugador, quiero luces que parpadeen en ciertas zonas para aumentar la inseguridad al explorar.",
             "RF-08: Las luces designadas deberan parpadear con intervalos pseudoaleatorios (0.1-1.5 s) sin superar 1 luz realtime activa por zona para proteger el rendimiento.",
             ["Parpadeo con ritmo irregular (no mecanico).",
              "Maximo 1 luz realtime por zona (verificado en profiler).",
              "El parpadeo no genera artefactos visuales (shadow acne)."],
             "Profiler de Unity: <= 2 draw calls adicionales por luz parpadeante; inspeccion visual de sombras.")),
]
