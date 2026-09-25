# CUESTIONARIO

## 1. ¿Qué significa la Planificación del Proyecto de Desarrollo de Software de Videojuegos?

La planificación de un proyecto de videojuego consiste en ordenar, antes de empezar a programar, todo lo que el equipo necesita saber para trabajar sin improvisar. Es la etapa donde se responde qué juego se va a construir, para quién, con qué personas y herramientas, en cuánto tiempo y de qué manera se comprobará que el resultado es el esperado. Planificar, entonces, es convertir una idea creativa en un trabajo organizado que cualquier miembro del equipo pueda consultar y seguir.

En la práctica, esta planificación se concreta en varias decisiones que quedan registradas desde el inicio:

- Se define el alcance del juego mediante una ficha de concepto, donde se fijan el género, el público al que va dirigido y el ciclo principal de juego, de manera que quede claro qué incluye el proyecto y qué queda fuera.
- Se construye el backlog, una lista priorizada de las características del juego con su estimación de esfuerzo, que pasa a ser la única fuente de trabajo del equipo [18].
- Se reparten los roles del equipo. En SUM existen el cliente, el productor interno, el equipo de desarrollo y el verificador beta, y cada uno tiene responsabilidades distintas durante el proyecto.
- Se divide el desarrollo en iteraciones cortas que terminan siempre en una versión jugable del juego, de modo que cada iteración funciona como un proyecto pequeño con su propia planificación, construcción y revisión [18].
- Se registra un conjunto de riesgos con su probabilidad, su impacto y la forma de enfrentarlos, y ese registro se revisa cada vez que termina una iteración.
- Cada característica se redacta como un requisito verificable con valores medibles, siguiendo la norma ISO/IEC/IEEE 29148 [2], para que su cumplimiento pueda probarse y no quede a interpretación.
- Finalmente, todo el avance se refleja en una herramienta de gestión, que en nuestro caso es el tablero de Jira.

Es importante aclarar que planificar no significa congelar el proyecto. En las metodologías ágiles el plan se revisa y se ajusta al final de cada iteración, porque se considera más valioso responder al cambio que seguir un plan rígido [19], aunque la estructura básica del marco de trabajo elegido se mantiene [18].

En nuestro proyecto PHOBIA: Ecos en la Oscuridad, la planificación quedó registrada en Jira. El trabajo se distribuyó en cinco sprints que agrupan veinticuatro historias de usuario con criterios de aceptación medibles, se fijaron los hitos H1 a H6 para marcar cada entregable y la matriz de riesgos permanece abierta durante todo el desarrollo. Así, tanto el equipo como el docente pueden verificar en cualquier momento qué partes del juego están terminadas y cuáles siguen pendientes.

## 2. ¿Qué etapas significativas presenta la metodología de desarrollo de videojuegos?

Un videojuego no se construye de una sola vez, sino que recorre un ciclo de vida que empieza con la idea inicial y termina mucho después del lanzamiento. En la industria este ciclo suele describirse en seis grandes etapas que van desde la pre-producción hasta el soporte posterior al lanzamiento, donde cada etapa termina con un artefacto concreto que se puede revisar, como el documento de diseño, el prototipo o la versión beta [20]. Para este laboratorio, el ciclo de vida se describe con las siguientes siete etapas:

| # | Etapa | Qué produce |
|---|---|---|
| 1 | Concepción | Idea, ficha de concepto, público objetivo y plataforma |
| 2 | Diseño | Documento de diseño del juego, mecánicas, arte conceptual y prototipo |
| 3 | Planificación | Roles, backlog, estimaciones, iteraciones, hitos y riesgos |
| 4 | Producción | Construcción iterativa del juego y versiones alfa jugables [20] |
| 5 | Pruebas | Versión alfa terminada y validación con usuarios en beta [20] |
| 6 | Distribución | Versión candidata a liberarse y lanzamiento |
| 7 | Mantenimiento | Parches, ajustes de balance y contenido posterior [20] |

Estas etapas siempre existen, pero la forma de recorrerlas depende de la metodología que el equipo elija. En proyectos pequeños y multidisciplinarios como el nuestro se aplica SUM, una metodología pensada para videojuegos que adapta la estructura de Scrum [18] y organiza el ciclo en cinco fases más una gestión de riesgos permanente:

| Fase SUM | Pregunta que responde | Producto mínimo |
|---|---|---|
| Concepto | ¿Qué juego construimos y para quién? | Ficha de concepto y prototipo |
| Planificación | ¿Cómo lo desarrollaremos y en cuánto tiempo? | Roles, backlog, iteraciones, hitos y riesgos |
| Elaboración iterativa | ¿Qué incremento jugable se construye en cada iteración? | Versión jugable incremental con funcionalidades aceptadas |
| Beta | ¿El juego funciona y la experiencia es aceptable? | Versión beta, plan de pruebas y reporte de defectos |
| Cierre | ¿Qué se entrega y qué aprendimos? | Versión final, manual y retrospectiva |
| Riesgos (transversal) | ¿Qué puede impedir cumplir los objetivos? | Matriz de riesgos actualizada por iteración |

Junto a SUM existen marcos complementarios. Scrum aporta la base de trabajo por sprints, el backlog y la definición de terminado [18]; el framework DPE se emplea cuando el juego persigue un objetivo serio como la educación o el entrenamiento [21]; y el análisis 5M permite clasificar los recursos del proyecto en método, medio, mano de obra, máquina y materiales, aunque no constituye una metodología de desarrollo por sí mismo.

En nuestro proyecto ya se completaron las fases de Concepto y Planificación, y actualmente el equipo se encuentra en la tercera iteración de Elaboración, trabajando en la entidad que persigue al jugador, los eventos de terror y la progresión de niveles. Las fases de Beta y Cierre quedaron programadas en el tablero de Jira con sus fechas y entregables, por lo que el recorrido del proyecto por cada etapa queda evidenciado de principio a fin.
