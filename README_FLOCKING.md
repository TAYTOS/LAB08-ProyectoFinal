# Guía de Configuración e Integración: Algoritmo de Flocking (Boids)
## Enjambre de Mini Bolitas Pequeñas para Unity

[![Algoritmo](https://img.shields.io/badge/Algoritmo-Flocking%20(Boids)-blue)](#)
[![Unity](https://img.shields.io/badge/Unity-2022.3%20%7C%20Unity%206-black?logo=unity)](#)
[![Render Pipeline](https://img.shields.io/badge/URP-Universal%20Render%20Pipeline-brightgreen)](#)
[![Estado](https://img.shields.io/badge/Estado-Listo%20para%20usar-success)](#)

Esta guía explica en detalle el funcionamiento, la arquitectura matemática y los pasos de configuración para integrar el **Algoritmo de Flocking** (desarrollado originalmente por Craig Reynolds en 1987) utilizando como caso de uso un **enjambre de mini bolitas pequeñas luminosas** dentro de las habitaciones y pasillos del proyecto.

---

## 📌 Tabla de Contenidos
1. [¿Qué es el Algoritmo de Flocking?](#-qué-es-el-algoritmo-de-flocking)
2. [Arquitectura del Sistema en el Proyecto](#-arquitectura-del-sistema-en-el-proyecto)
3. [Instalación Rápida (1 Clic)](#-instalación-rápida-1-clic)
4. [Configuración Manual Paso a Paso](#-configuración-manual-paso-a-paso)
5. [Guía de Parámetros del Inspector (`FlockManager`)](#-guía-de-parámetros-del-inspector-flockmanager)
6. [Integración con `SteeringAgent` (Monstruos y Enemigos)](#-integración-con-steeringagent-monstruos-y-enemigos)
7. [Preguntas Frecuentes y Ajustes Típicos (FAQ)](#-preguntas-frecuentes-y-ajustes-típicos-faq)

---

## 🔬 ¿Qué es el Algoritmo de Flocking?

El algoritmo de *Flocking* simula el movimiento coordinado de bandadas de aves, bancos de peces o enjambres de insectos/partículas. Cada individuo (denominado *Boid* o mini bolita) no sigue a un líder centralizado, sino que toma decisiones locales basadas en tres leyes elementales:

```
      SEPARACIÓN                    ALINEACIÓN                     COHESIÓN
  (Evitar colisiones)         (Misma dirección/velocidad)      (Permanecer juntos)

       <- o ->                         ^    ^                        o   -->
         / \                           |   /                       /   \   o
        v   v                         |  /                       o  <- o
 (Repulsión cercana)             (Velocidad promedio)       (Centro de masa común)
```

### 1. Separación (Separation)
Evita el hacinamiento y colisiones con compañeros cercanos. Genera una fuerza repulsiva inversamente proporcional a la distancia:
$$\vec{F}_{sep} = \sum_{j \in \text{vecinos}} \frac{\vec{p}_i - \vec{p}_j}{|\vec{p}_i - \vec{p}_j|^2}$$

### 2. Alineación (Alignment)
Ajusta la velocidad y orientación para igualar la velocidad promedio del grupo local:
$$\vec{v}_{avg} = \frac{1}{N} \sum_{j \in \text{vecinos}} \vec{v}_j, \quad \vec{F}_{ali} = (\vec{v}_{avg}^{norm} \cdot v_{max}) - \vec{v}_i$$

### 3. Cohesión (Cohesion)
Desplaza al individuo hacia el centro de masa (posición promedio) de los compañeros cercanos:
$$\vec{p}_{center} = \frac{1}{N} \sum_{j \in \text{vecinos}} \vec{p}_j, \quad \vec{F}_{coh} = \text{Seek}(\vec{p}_{center})$$

### Extensiones Implementadas
* **Interacción con el Jugador (Target)**: Las mini bolitas pueden perseguir al jugador (`Seek`), huir (`Flee`), o realizar **dispersión de pánico** (`ScatterOnApproach`): vuelan juntas en grupo, pero si el jugador se acerca a menos de 4 metros se dispersan asustadas hacia todas direcciones y luego vuelven a agruparse.
* **Confinamiento (Boundaries)**: Se mantienen dentro de una caja (`Box`), esfera (`Sphere`) o dentro de las dimensiones de una habitación (`RoomData`).
* **Evasión de Obstáculos (Raycasting)**: Esferas sensoriales detectan paredes y rebotan suavemente sin atravesarlas.
* **Aspecto Visual**: Bolitas bioluminiscentes (URP Lit con emisión y `TrailRenderer`) ideales para la atmósfera oscura de terror psicológico.

---

## 📂 Arquitectura del Sistema en el Proyecto

Los scripts se organizan de forma desacoplada y limpia:

| Archivo | Ubicación | Descripción |
| :--- | :--- | :--- |
| [`BoidAgent.cs`](file:///c:/Users/gabo/Desktop/REPOS/DEV/LAB08-ProyectoFinal/Assets/Scripts/Flocking/BoidAgent.cs) | `Assets/Scripts/Flocking/` | Componente de cada mini bolita individual. Calcula fuerzas locales e integra el movimiento cinético. |
| [`FlockManager.cs`](file:///c:/Users/gabo/Desktop/REPOS/DEV/LAB08-ProyectoFinal/Assets/Scripts/Flocking/FlockManager.cs) | `Assets/Scripts/Flocking/` | Gestor del enjambre. Instancia las mini bolitas, gestiona pesos, objetivos y confinamiento. |
| [`FlockEditorMenu.cs`](file:///c:/Users/gabo/Desktop/REPOS/DEV/LAB08-ProyectoFinal/Assets/Editor/FlockEditorMenu.cs) | `Assets/Editor/` | Menú de Unity para generar el enjambre completo con 1 solo clic. |
| [`AlignmentBehavior.cs`](file:///c:/Users/gabo/Desktop/REPOS/DEV/LAB08-ProyectoFinal/Assets/Scripts/SteeringBehaviors/AlignmentBehavior.cs) | `Assets/Scripts/SteeringBehaviors/` | Comportamiento modular de alineación para cualquier `SteeringAgent` (monstruos/enemigos). |
| [`CohesionBehavior.cs`](file:///c:/Users/gabo/Desktop/REPOS/DEV/LAB08-ProyectoFinal/Assets/Scripts/SteeringBehaviors/CohesionBehavior.cs) | `Assets/Scripts/SteeringBehaviors/` | Comportamiento modular de cohesión para cualquier `SteeringAgent`. |
| [`FlockingBehavior.cs`](file:///c:/Users/gabo/Desktop/REPOS/DEV/LAB08-ProyectoFinal/Assets/Scripts/SteeringBehaviors/FlockingBehavior.cs) | `Assets/Scripts/SteeringBehaviors/` | Comportamiento "3 en 1" que unifica Separación, Alineación y Cohesión en `SteeringAgent`. |

---

## ⚡ Instalación Rápida (1 Clic)

No necesitas crear prefabs ni configurar shaders a mano. Ya tienes una herramienta de editor integrada:

1. En Unity, abre cualquier escena (ej. `GameScene`, `TestingScene` o una escena nueva).
2. En la barra de menú superior, haz clic en:
   **`Tools` > `IA - Steering` > `Crear Enjambre de Mini Bolitas (Flocking)`**
   *(o clic derecho en la ventana Jerarquía > `3D Object` > `Enjambre Flocking (Mini Bolitas)`)*.
3. Se creará automáticamente un GameObject llamado **`Flock_MiniBolitas`** colocado en la escena.
4. Presiona el botón **PLAY** ▶️ de Unity:
   * Aparecerán automáticamente **40 mini bolitas luminosas** con estelas cian.
   * Si tu jugador existe en la escena, las bolitas reaccionarán a su presencia: al acercarte se dispersarán como luciérnagas asustadas y volverán a unirse en formación.

---

## 🛠️ Configuración Manual Paso a Paso

Si prefieres crearlo manualmente o personalizarlo:

### Paso 1: Crear el Gestor del Enjambre
1. En la Jerarquía, clic derecho > **Create Empty**. Nómbralo `Enjambre_Bolitas`.
2. Posiciónalo donde desees que aparezca el grupo (por ejemplo: `X: 0, Y: 1.5, Z: 0`).
3. Añade el script **`FlockManager`** al GameObject (`Add Component` > `FlockManager`).

### Paso 2: Configurar el Prefab (Opcional)
* **Si dejas el campo `Boid Prefab` vacío (None)**: El script creará automáticamente esferas procedimentales pequeñas (`0.25m`), les colocará un material URP brillante con emisión y un `TrailRenderer`.
* **Si quieres tu propio modelo/prefab**:
  1. Crea una esfera pequeña (`3D Object` > `Sphere`), cambia su escala a `(0.2, 0.2, 0.2)`.
  2. En su `Collider`, marca la casilla **`Is Trigger`** (para que no bloquee al jugador ni empuje objetos físicos).
  3. Arrástralo a tu carpeta `Assets/Prefabs/` para guardarlo como Prefab.
  4. Arrastra ese Prefab al campo **`Boid Prefab`** en el `FlockManager`.

### Paso 3: Asignar el Objetivo (Jugador)
* Por defecto, el `FlockManager` busca automáticamente un objeto con el Tag **`Player`**.
* Si deseas que sigan una linterna, una píldora calmante o un punto específico, arrastra dicho Transform al campo **`Target`**.

---

## 🎛️ Guía de Parámetros del Inspector (`FlockManager`)

| Sección | Parámetro | Valor Sugerido | Descripción |
| :--- | :--- | :--- | :--- |
| **Configuración** | `Flock Size` | `30` a `60` | Número de mini bolitas generadas. |
| | `Bolita Scale` | `0.25` | Tamaño (diámetro) de cada bolita en metros. |
| | `Bolita Color` | Cian / Amarillo | Color base y resplandor emisivo en la oscuridad. |
| | `Enable Trails` | `True` | Activa estelas luminosas detrás de cada bolita. |
| | `Is 3D` | `True` | Si está activo, vuelan en 3D volumétrico. Si es `False`, se desplazan a ras de suelo. |
| **Pesos Reynolds**| `Separation Weight` | `1.5` | Fuerza para evitar que choquen entre sí. Debe ser mayor que Cohesión para evitar apilamientos. |
| | `Alignment Weight` | `1.0` | Fuerza para volar hacia la misma dirección. |
| | `Cohesion Weight` | `1.0` | Fuerza de atracción para mantenerse en grupo. |
| **Radios** | `Neighbor Radius` | `3.5` | Distancia visual a la que cada bolita reconoce a sus compañeras. |
| | `Separation Radius`| `0.85` | Distancia crítica de repulsión inmediata entre bolitas. |
| **Velocidades** | `Min Speed` | `2.0` | Velocidad mínima de crucero (evita que queden congeladas). |
| | `Max Speed` | `5.5` | Velocidad tope de desplazamiento. |
| | `Max Steer Force` | `8.0` | Capacidad de maniobra y giro (a mayor valor, giros más rápidos). |
| **Objetivo** | `Target Mode` | `ScatterOnApproach`| Modo de interacción: `Seek` (perseguir), `Flee` (huir), `ScatterOnApproach` (dispersión al acercarse) o `Ignore`. |
| | `Flee Distance` | `4.0` | Distancia del jugador a la que las bolitas entran en pánico y se dispersan. |
| **Límites** | `Boundary Type` | `Box` o `Sphere` | Forma del volumen de confinamiento para que no se escapen al infinito. |
| | `Bounds Box Size` | `(14, 4, 14)` | Dimensiones del área donde el enjambre tiene permitido volar. |
| | `Bound Room` | *(Opcional)* | Arrastra un `RoomData` de la escena procedural para confinarlas dentro de ese cuarto. |
| **Obstáculos** | `Avoid Obstacles` | `True` | Detecta paredes con raycasts y esquiva superficies sólidas. |

---

## 👾 Integración con `SteeringAgent` (Monstruos y Enemigos)

Si además de las mini bolitas deseas que los **monstruos o criaturas** del juego formen manadas utilizando la misma arquitectura de Steering Behaviors existente:

El proyecto ya incluía `SeparationBehavior.cs` (Separación). Ahora tienes a tu disposición:

1. **`AlignmentBehavior.cs`**:
   * Agrégalo al enemigo con `SteeringAgent`.
   * Los monstruos se orientarán hacia la dirección promedio de sus compañeros.
2. **`CohesionBehavior.cs`**:
   * Agrégalo al enemigo.
   * Los monstruos se mantendrán juntos en patrulla o persecución grupal.
3. **`FlockingBehavior.cs` (Recomendado para monstruos en grupo)**:
   * Agrupa Separación, Alineación y Cohesión en un único componente con pesos individuales ajustables.
   * Se suma automáticamente a la aceleración total en `SteeringAgent.LateUpdate()` respetando el NavMesh del laberinto.

---

## ❓ Preguntas Frecuentes y Ajustes Típicos (FAQ)

### ¿Las bolitas atraviesan las paredes del laberinto?
Asegúrate de que:
1. `Avoid Obstacles` esté marcado como **`True`** en el `FlockManager`.
2. En `Obstacle Mask`, incluye las capas de tus paredes (por defecto `Everything` o la capa `Default`).
3. El `Bounds Box Size` no sea más grande que la habitación donde se ubican. Si estás en una habitación procedural, arrastra el componente `RoomData` al campo `Bound Room`.

### ¿Cómo hacer que las bolitas vuelen al ras de piso como pequeñas arañas o robots?
Desmarca la casilla **`Is 3D`** en el `FlockManager`. Al hacer esto, todas las fuerzas en el eje vertical $Y$ se anulan y las bolitas se desplazan en el plano $XZ$.

### ¿Cómo cambiar el color de las bolitas en tiempo de ejecución?
Puedes cambiar el color directamente desde el Inspector en `Bolita Color`, o por código:
```csharp
flockManager.bolitaColor = Color.magenta;
```

### ¿Cómo hacer que las bolitas sigan la linterna o la luz del jugador?
Arrastra el GameObject de la luz o linterna al campo **`Target`** del `FlockManager` y cambia `Target Mode` a **`Seek`**.

---

¡Disfruta del algoritmo de Flocking en tu proyecto! Si necesitas ajustar algún comportamiento adicional, todos los scripts cuentan con comentarios explicativos listos para extender.
