# -*- coding: utf-8 -*-
"""Historias HU-17 a HU-24 + trazabilidad (links) entre Beta y Cierre."""
from jira_lib import p, h, bullets
from jira_issues_b import hu_desc

ISSUES_D = [
    dict(id="HU-17", type="Story", epic=2, sprint=2, status="todo", pts=5,
         prio="Medium", labels=["gameplay", "iter-3"], version=None, comments=[],
         summary="Multiples evidencias y lista de objetos a encontrar por nivel",
         desc=hu_desc(
             "Como jugador, quiero una lista de varios objetos ocultos por nivel para tener mas retos que un solo objeto.",
             "RF-17: El sistema debera generar por nivel una lista de 3 a 5 objetos ocultos unicos, registrar cuales fueron encontrados y habilitar la victoria solo al completar la lista.",
             ["Cada nivel presenta entre 3 y 5 objetos distintos.",
              "El HUD marca en la lista cada objeto encontrado.",
              "La victoria se bloquea hasta completar la lista completa."],
             "Prueba funcional: encontrar objetos parciales (2/4) y verificar que la victoria permanece bloqueada.")),
    dict(id="HU-18", type="Story", epic=2, sprint=2, status="todo", pts=3,
         prio="Low", labels=["gameplay", "iter-3"], version=None, comments=[],
         summary="Sistema de puntuacion por nivel",
         desc=hu_desc(
             "Como jugador, quiero recibir puntaje segun tiempo restante y ansiedad controlada para rejugabilidad.",
             "RF-18: Al completar un nivel, el sistema debera calcular puntaje = (segundos restantes x 10) + (100 - ansiedad final) x 5, y mostrarlo en la pantalla de victoria.",
             ["El puntaje se calcula con la formula exacta indicada.",
              "El puntaje se muestra en la pantalla de victoria.",
              "El mejor puntaje por nivel se guarda entre sesiones (PlayerPrefs)."],
             "Prueba unitaria de la formula con 5 casos + prueba de guardado/carga del mejor puntaje.")),

    # ---------------- BETA ----------------
    dict(id="HU-19", type="Task", epic=3, sprint=3, status="todo", pts=3,
         prio="Highest", labels=["SUM", "beta", "pruebas"], version=2, comments=[],
         summary="Plan de pruebas beta y build v0.9.0-beta",
         desc=[
             h(2, "Plan de pruebas (SUM - Fase Beta)"),
             bullets([
                 "Casos de prueba: uno por cada RF-01 a RF-18 con resultado esperado y evidencia.",
                 "RNF-01 (rendimiento): la escena inicia en <= 3 s y el juego corre a >= 60 FPS en el equipo de referencia; cronometrar 5 ejecuciones y registrar el maximo.",
                 "RNF-02 (usabilidad): un usuario nuevo identifica los controles basicos sin ayuda en <= 60 s (prueba con 3 usuarios).",
                 "Sesion pensada de 15-20 min por usuario con formulario de observaciones.",
             ]),
             p("Entregable: build v0.9.0-beta + documento de plan de pruebas."),
         ]),
    dict(id="HU-20", type="Task", epic=3, sprint=3, status="todo", pts=5,
         prio="Highest", labels=["SUM", "beta", "pruebas"], version=2, comments=[],
         summary="Sesiones beta con 5 usuarios externos y registro de defectos",
         desc=[
             h(2, "Verificacion beta"),
             p("Los verificadores beta (usuarios externos, distintos de quien implemento cada caracteristica) juegan la build v0.9.0-beta y reportan defectos y observaciones de experiencia."),
             h(3, "Criterios de aceptacion"),
             bullets([
                 ">= 5 sesiones registradas con fecha, duracion y usuario.",
                 "Todos los defectos reportados como issues en este proyecto con severidad.",
                 "Cuestionario de experiencia: tension, claridad de objetivos y controles (escala 1-5).",
             ]),
         ]),
    dict(id="HU-21", type="Task", epic=3, sprint=3, status="todo", pts=5,
         prio="High", labels=["SUM", "beta", "defectos"], version=2, comments=[],
         blocked_by=["HU-20"],
         summary="Correccion de defectos criticos de beta y ajustes finales",
         desc=[
             h(2, "Correccion de defectos"),
             p("Se corrigen primero los defectos de severidad Critica y Alta reportados en HU-20, y se aplican ajustes de balance (ansiedad, tiempos) segun observaciones."),
             h(3, "Criterios de aceptacion"),
             bullets([
                 "0 defectos criticos abiertos al cerrar el sprint.",
                 "Nueva build v0.9.1 con las correcciones, verificada con los mismos casos de prueba.",
             ]),
         ]),

    # ---------------- CIERRE ----------------
    dict(id="HU-22", type="Task", epic=4, sprint=4, status="todo", pts=3,
         prio="Highest", labels=["SUM", "cierre"], version=3, comments=[],
         blocked_by=["HU-21"],
         summary="Build final v1.0.0 y paquete de entrega",
         desc=[
             h(2, "Entrega (SUM - Fase Cierre)"),
             bullets([
                 "Build ejecutable Windows v1.0.0-release generada desde la rama principal.",
                 "Paquete final: build + codigo fuente + informe + este tablero como evidencia de gestion.",
             ]),
         ]),
    dict(id="HU-23", type="Task", epic=4, sprint=4, status="todo", pts=2,
         prio="Medium", labels=["SUM", "cierre", "documento"], version=3, comments=[],
         summary="Manual breve de usuario",
         desc=[
             h(2, "Manual breve"),
             bullets([
                 "Instalacion y requisitos minimos del equipo.",
                 "Controles: WASD, mouse, E (interactuar), Escape (pausa).",
                 "Objetivo del juego, sistema de ansiedad y consejos de supervivencia.",
             ]),
         ]),
    dict(id="HU-24", type="Task", epic=4, sprint=4, status="todo", pts=2,
         prio="High", labels=["SUM", "cierre", "retro"], version=3, comments=[],
         summary="Retrospectiva final y acta de cierre",
         desc=[
             h(2, "Retrospectiva (SUM - Fase Cierre)"),
             bullets([
                 "Mantener: revisiones de iteracion con cliente y DoD escrita.",
                 "Dejar de hacer: estimar historias de 8 pts sin dividirlas.",
                 "Empezar a hacer: pruebas de rendimiento desde la primera build.",
                 "Lecciones aprendidas y comparativa de estimado vs. real por iteracion.",
                 "Acta de cierre firmada por el equipo (Paulo Hidalgo y Gabriel Bernedo).",
             ]),
         ]),
]
