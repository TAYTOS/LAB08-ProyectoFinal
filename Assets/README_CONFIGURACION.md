# Guía de Configuración de Escenas - Proyecto Backrooms

Esta guía te ayudará a configurar tu escena de Unity desde cero para que todas las mecánicas procedimentales, de terror psicológico (ansiedad/fobias) y persecución con Inteligencia Artificial funcionen a la perfección.

---

## 1. Configuración del Generador de Nivel Procedural

El núcleo del entorno. Generará tu laberinto de forma infinita cada vez que des Play.

1.  En tu escena vacía, haz click derecho en la jerarquía (Hierarchy) y selecciona **Create Empty**. Llámalo `LevelGenerator`.
2.  Arrastra el script **`ProceduralLevelGenerator.cs`** a este objeto.
3.  **Configuración en el Inspector**:
    *   **Map Width / Depth**: Controlan qué tan grande es el laberinto. (Por defecto 30x30).
    *   **Prefabs**: Si los dejas vacíos, el script instanciará *Cubos de Unity* con colores Backrooms (crema/amarillo). Si tienes prefabs de muros/suelos listos, arrástralos aquí.
    *   *Nota:* No te preocupes por el "NavMesh" para que los monstruos caminen, este script lo crea y "hornea" automáticamente cuando termina de generar el nivel.

---

## 2. Configuración del Jugador y el Gestor de Ansiedad

El jugador es quien sufrirá las consecuencias de la geometría y los monstruos.

1.  Crea o usa tu Player (Ej: el que tiene la cámara de Primera Persona o de donde cuelga el modelo del personaje).
2.  Asegúrate de que tenga:
    *   Un **Collider** (Ej. `CapsuleCollider`).
    *   Un **Rigidbody** (Marca la casilla `Is Kinematic` si mueves al jugador por código o CharacterController, así detectará los triggers invisibles de los cuartos sin salir volando por las físicas).
    *   La etiqueta (**Tag**) seteada en `Player` en la parte superior del inspector.
3.  Arrastra el script **`AnxietyManager.cs`** al Jugador.
4.  **Configuración de Fobias en el Inspector**:
    *   Despliega `Active Phobias`, añade elementos (`+`) y selecciona los que quieras (Ej. `Claustrophobia`, `Nyctophobia`, `Automatophobia`).
    *   **Entity Prefab To Spawn**: Arrastra aquí el **Prefab de tu monstruo** (Ej. El Gato Tétrico). Este es el monstruo que aparecerá por sorpresa si la Ansiedad llega al máximo.

---

## 3. Configuración de Entidades Enemigas (Monstruos / Luigi / Gato)

Aquí configuraremos a los enemigos para que te persigan inteligentemente y te den miedo.

1.  Abre el Prefab de tu enemigo.
2.  **Persecución Inteligente (NavMesh)**:
    *   Arrastra el script **`SeguirJugador.cs`**.
    *   Verás que Unity le añade automáticamente un componente llamado **`NavMesh Agent`**.
    *   En el `NavMesh Agent`, ajusta la `Speed` (velocidad) para decidir qué tan rápido corre.
    *   En `SeguirJugador`, ajusta el `Distancia Frenado` (para que no te traspase) y `Ajuste Altura` si el gato flota.
3.  **Dar Miedo (Phobia Trigger)**:
    *   Arrastra el script **`PhobiaTrigger.cs`** a tu enemigo o a un objeto perturbador inanimado.
    *   En `Phobia Tag`, selecciona `Automatophobia`.
    *   Ajusta el `Effect Radius`. Cuando el jugador entre a ese radio de distancia del enemigo, su ansiedad subirá rápidamente.

---

## 4. Flujo de Juego Esperado al dar PLAY

1.  El `ProceduralLevelGenerator` crea el laberinto, coloca colores, triggers por habitación y genera el NavMesh (las rutas de inteligencia artificial).
2.  Tu jugador aparecerá (idealmente deberías instanciar o mover a tu jugador al punto verde brillante `START ZONE`).
3.  Mientras caminas:
    *   Si entras a cuartos pequeños y tienes *Claustrofobia*, tu ansiedad subirá.
    *   Si ves al gato a lo lejos y tienes *Automatofobia*, tu ansiedad subirá.
4.  Si la ansiedad alcanza el 80% (o el límite que configuraste), pasará algo aleatorio: ¡O el cuarto de atrás se vuelve totalmente oscuro, o un segundo gato espeluznante aparece detrás de ti persiguiéndote por el NavMesh!

¡Con esto ya tienes un juego con mecánicas procedimentales, IA y terror psicológico listos!
