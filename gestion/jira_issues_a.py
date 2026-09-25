# -*- coding: utf-8 -*-
"""Issues de las fases Concepto, Planificacion y Gestion de Riesgos (SUM)."""
from jira_lib import p, h, bullets

ISSUES_A = [
    # ---------------- CONCEPTO ----------------
    dict(id="C-01", type="Task", epic=0, sprint=0, status="done", pts=2,
         prio="Highest", labels=["SUM", "concepto"], version=0, comments=[
            "Aprobado por el cliente (docente) en la reunion del 10/08. Se valida el core loop y la propuesta de valor."],
         summary="Ficha de concepto del juego",
         desc=[
             h(2, "Ficha de concepto (SUM - Fase Concepto)"),
             bullets([
                 "Nombre: PHOBIA: Ecos en la Oscuridad",
                 "Genero: Terror / Hidden Object 3D en primera persona",
                 "Publico objetivo: jugadores casuales de 13+ anos",
                 "Plataforma: PC - Windows | Motor: Unity 6 (URP)",
                 "Core loop: Explorar -> gestionar ansiedad -> encender luces/interactuar -> encontrar evidencias -> escapar antes de que acabe el tiempo",
                 "Mecanica diferencial: ansiedad dinamica (audio de latido + efectos visuales) y niveles generados proceduralmente con semilla",
                 "Criterio de exito del prototipo: el jugador completa un nivel de 3-5 minutos encontrando el objeto clave sin errores criticos",
             ]),
             p("Producto minimo de la fase: 1 pagina + boceto/prototipo de concepto (escena de habitacion jugable, ver C-02)."),
         ]),
    dict(id="C-02", type="Task", epic=0, sprint=0, status="done", pts=3,
         prio="High", labels=["SUM", "concepto", "prototipo"], version=0,
         comments=["Prototipo validado en Laboratorio 08 de CGVCM: escena jugable con iluminacion, Raycast y temporizador."],
         summary="Boceto y prototipo de concepto (escena habitacion)",
         desc=[
             h(2, "Prototipo de concepto"),
             p("Escena interactiva de habitacion cerrada (asset Cosmic Retro Station) con controlador FPS, interruptor de luz con tecla E y botiquin oculto. Evidencia: LABORATORIO 08 CGVCM.pdf y repositorio Git del equipo."),
             h(3, "Criterio de aceptacion"),
             bullets([
                 "El jugador navega la habitacion en primera persona sin atravesar colisiones.",
                 "El interruptor enciende/apaga la luz y el objeto clave solo es visible con la luz encendida.",
                 "Se genera la emocion objetivo (miedo/tension) mediante iluminacion y sonido ambiente.",
             ]),
         ]),

    # ---------------- PLANIFICACION ----------------
    dict(id="P-01", type="Task", epic=1, sprint=0, status="done", pts=2,
         prio="Highest", labels=["SUM", "planificacion", "roles"], version=0,
         comments=["Roles comunicados al cliente y registrados en la descripcion del proyecto."],
         summary="Definicion de roles SUM del equipo",
         desc=[
             h(2, "Roles asignados (SUM)"),
             bullets([
                 "Cliente: docente del curso DSJ (define prioridades y acepta resultados).",
                 "Productor interno (equivalente a Scrum Master): Paulo Hidalgo - coordina proceso, riesgos, cronograma y comunicacion.",
                 "Equipo de desarrollo: Paulo Hidalgo (programacion, diseno de niveles) y Gabriel Bernedo Kaseng (arte, audio, pruebas).",
                 "Verificador beta: 5 usuarios externos (companeros de otros grupos), distintos de quien implemento cada caracteristica.",
             ]),
             p("Nota: Taylor Betanzos pertenece a otro grupo de teoria y no participa en este proyecto."),
         ]),
    dict(id="P-02", type="Task", epic=1, sprint=0, status="done", pts=3,
         prio="Highest", labels=["SUM", "planificacion", "backlog"], version=0,
         comments=[],
         summary="Construccion del Product Backlog priorizado",
         desc=[
             h(2, "Product Backlog"),
             p("Backlog de caracteristicas priorizado (Alta/Media/Baja) con estimaciones en puntos de historia y criterios de aceptacion verificables por historia (HU-01 a HU-24), alineado a las iteraciones ITER-1..ITER-3, Beta y Cierre."),
             h(3, "Criterio de aceptacion"),
             bullets([
                 "Toda historia tiene prioridad, estimacion en puntos y criterio de aceptacion medible.",
                 "Cada historia referencia requisitos RF/RNF trazables (ISO/IEC/IEEE 29148:2018).",
                 "El backlog esta visible en este tablero Jira y ordenado por prioridad.",
             ]),
         ]),
    dict(id="P-03", type="Task", epic=1, sprint=0, status="done", pts=2,
         prio="High", labels=["SUM", "planificacion", "cronograma"], version=0,
         comments=[],
         summary="Cronograma: iteraciones, hitos y entregables",
         desc=[
             h(2, "Iteraciones (sprints de 2 semanas)"),
             bullets([
                 "ITER-1 (10/08-24/08): Nucleo jugable -> build v0.1.0-alpha-it1",
                 "ITER-2 (24/08-07/09): Atmosfera y sistemas -> build v0.2.0-alpha-it2",
                 "ITER-3 (07/09-28/09): Contenido y progresion -> feature complete",
                 "SPRINT BETA (28/09-05/10): plan de pruebas, sesiones y correcciones -> v0.9.0-beta",
                 "SPRINT CIERRE (05/10-12/10): build final, manual y retrospectiva -> v1.0.0-release",
             ]),
             h(2, "Hitos"),
             bullets([
                 "H1 (10/08) Concepto aprobado", "H2 (24/08) Alpha nucleo jugable",
                 "H3 (07/09) Alpha atmosfera/sistemas", "H4 (28/09) Feature complete",
                 "H5 (05/10) Beta validada", "H6 (12/10) Release final",
             ]),
         ]),
    dict(id="P-04", type="Task", epic=1, sprint=0, status="done", pts=1,
         prio="Medium", labels=["SUM", "planificacion", "DoD"], version=0, comments=[],
         summary="Definition of Done (DoD) del equipo",
         desc=[
             h(2, "Definition of Done"),
             bullets([
                 "Codigo integrado en la rama principal sin errores de compilacion.",
                 "Funcionalidad probada segun sus criterios de aceptacion.",
                 "No introduce defectos criticos conocidos.",
                 "Assets con nombres y carpetas consistentes.",
                 "Commit identificado y tablero actualizado.",
                 "Build ejecutable disponible cuando corresponda.",
             ]),
             p("Regla: una tarjeta solo pasa a Hecho cuando cumple su criterio de aceptacion y esta DoD."),
         ]),
    dict(id="P-05", type="Task", epic=1, sprint=0, status="done", pts=1,
         prio="Medium", labels=["SUM", "planificacion", "herramientas"], version=0,
         comments=["Tablero Jira configurado con columnas Por hacer / En curso / En prueba / Hecho y sprints creados."],
         summary="Configuracion de herramientas de gestion (Jira + Git)",
         desc=[
             h(2, "Herramientas"),
             bullets([
                 "Jira (este proyecto PHO): backlog, sprints, tablero y evidencias de avance.",
                 "Git/GitHub: repositorio del codigo fuente Unity con commits identificables por historia.",
                 "Unity 6 URP: motor de desarrollo; builds ejecutables por iteracion.",
             ]),
         ]),

    # ---------------- GESTION DE RIESGOS ----------------
    dict(id="R-REG", type="Task", epic=5, sprint=None, status="progress", pts=3,
         prio="Highest", labels=["SUM", "riesgos"], version=None,
         comments=[
             "[ITER-1] Matriz creada. Riesgo activo principal: rendimiento con luces dinamicas (R-02). Accion: combinar iluminacion horneada con pocas luces en tiempo real.",
             "[ITER-2] R-02 mitigado parcialmente (FlickeringLight con una sola luz realtime por zona). Nuevo foco: R-03 disponibilidad del equipo (2 integrantes).",
             "[ITER-3] R-04 reducido: generator con control de probabilidades y semillas (commits del repo). Se mantiene monitoreo semanal en cada revision de iteracion.",
         ],
         summary="Registro de riesgos del proyecto (matriz viva)",
         desc=[
             h(2, "Matriz de riesgos (probabilidad / impacto / mitigacion / contingencia)"),
             h(3, "R-01 Sobrealcance (scope creep)"),
             p("Probabilidad: Media | Impacto: Alto. Mitigacion: backlog priorizado y congelar alcance por iteracion. Contingencia: mover historias de prioridad Baja a version posterior."),
             h(3, "R-02 Rendimiento con multiples luces dinamicas"),
             p("Probabilidad: Alta | Impacto: Alto. Mitigacion: limitar luces realtime, sombras horneadas y pruebas de FPS por build. Contingencia: reducir calidad de sombras y cantidad de efectos de post-procesado."),
             h(3, "R-03 Equipo reducido (2 integrantes)"),
             p("Probabilidad: Media | Impacto: Alto. Mitigacion: subroles explicitos y pair programming en historias de 8 pts. Contingencia: descartar caracteristicas opcionales (puntaje/ranking)."),
             h(3, "R-04 Fallos de la generacion procedural"),
             p("Probabilidad: Media | Impacto: Medio. Mitigacion: control de probabilidades, semillas fijas para pruebas y salas seguras garantizadas. Contingencia: fallback a 3 niveles disenados a mano."),
             h(3, "R-05 Licencias de assets de terceros"),
             p("Probabilidad: Baja | Impacto: Medio. Mitigacion: usar assets gratuitos de Unity Asset Store con licencia verificada. Contingencia: reemplazar por modelos propios low-poly."),
             p("La matriz se revisa y actualiza al cierre de cada iteracion (ver comentarios)."),
         ]),
]
