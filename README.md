# 🍲 A Toda Olla

<p align="center">
  <img src="Assets/UI/MainMenu/MenuPrincipal.png" alt="A Toda Olla Banner" width="100%" style="border-radius: 12px; box-shadow: 0 8px 24px rgba(0,0,0,0.3);" />
</p>

<p align="center">
  <strong>Simulador de conducción, logística y reparto de comida en primera persona desarrollado en Unity 6 (URP).</strong>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Unity-6000.x%20(Unity%206)-black?style=for-the-badge&logo=unity" alt="Unity Version" />
  <img src="https://img.shields.io/badge/Render%20Pipeline-URP-blue?style=for-the-badge&logo=unity" alt="Render Pipeline" />
  <img src="https://img.shields.io/badge/Language-C%23-239120?style=for-the-badge&logo=csharp" alt="C#" />
  <img src="https://img.shields.io/badge/Input%20System-New%20Input%20System-orange?style=for-the-badge" alt="New Input System" />
  <img src="https://img.shields.io/badge/Platform-PC%20%2F%20Mac-lightgrey?style=for-the-badge" alt="Platforms" />
  <img src="https://img.shields.io/badge/Status-In%20Active%20Development-success?style=for-the-badge" alt="Status" />
</p>

---

## 📖 Acerca del Juego

**A Toda Olla** es un videojuego de simulación en primera persona donde asumes el rol de repartidor en una vibrante ciudad de estilo latinoamericano. Tu misión: transportar ollas de comida caliente a contrarreloj a bordo de tu furgoneta de repartos, administrando combustible, dinero, mejoras mecánicas y el cuidado de la carga.

Todo esto acompañado de una radio vehicular interactiva repleta de clásicos musicales (cumbia, salsa, reguetón y baladas) para amenizar cada turno laboral.

---

## ✨ Características Principales

### 🚐 1. Física de Vehículo y Conducción Inmersiva
- **Control dinámico de furgoneta:** Aceleración por marchas, RPM de motor, inercia de frenado, suspensión independiente y consumo realista de combustible.
- **Instrumentación completa:** Velocímetro analógico/digital integrado en HUD con aguja reactiva e indicador de nivel de gasolina (`SpeedometerHUD`).
- **Interacción física con el vehículo:** Puertas traseras abatibles mediante palancas interactivas, entrada/salida fluida de la cabina y compartimento de carga hueco (`VanDoorController`, `VanHollowColliders`).

### 📦 2. Sistema de Carga e Interacción en Primera Persona
- **Manipulación de objetos con física:** Agarre, rotación, transporte e inspección en primera persona de ollas y paquetes (`PlayerPickup`, `PickableItem`).
- **Zona de carga organizada:** La zona posterior de la furgoneta detecta la colocación ordenada de la mercancía mediante slots magnéticos y físicos (`CargoZone`, `CargoSlotLayout`).
- **Puntos de entrega dinámicos:** Balizas luminosas y marcadores visuales que verifican la llegada de cada pedido (`DeliveryPoint`).

### 🔧 3. Taller Mecánico y Sistema de Mejoras
- **Zona de taller interactiva:** Acude al taller de la ciudad para reparar tu furgoneta o mejorar su rendimiento (`MechanicWorkshopZone`).
- **Árbol de mejoras por estrellas:** Aumenta la velocidad máxima, aceleración, capacidad de combustible y potencia de frenado (`VehicleUpgradeManager`, `MechanicWorkshopUI`).

### 🗺️ 4. Navegación Avanzada (GPS y Mapa Grande)
- **Minimapa en tiempo real:** Orientación circular fija o rotativa en pantalla con rastreo de la furgoneta y objetivos.
- **Gran Mapa Interactivo (Big Map):** Vista satelital completa con zoom, desplazamiento, puntos de interés (restaurantes, taller, clientes) y rutas guiadas (`BigMapController`).
- **Brújula 3D y waypoints:** Indicador HUD con distancia en metros y dirección hacia la próxima entrega (`DeliveryNavigationHUD`).

### 💼 5. Jornadas Laborales y Economía (Turnos)
- **Gestión de turnos de trabajo:** Límites de tiempo, cuotas de pedidos diarios y reloj dinámico (`ShiftManager`, `ShiftHUD`).
- **Pantalla de resultados:** Resumen financiero al finalizar la jornada (ingresos brutos, propinas, gasto de gasolina, penalizaciones y progreso) (`ShiftResultsUI`).
- **Contador de dinero animado:** HUD reactivo que celebra ganancias y descuenta costes de mantenimiento (`DineroHUD`).

### 📻 6. Radio Vehicular Auténtica
- Reproductor estéreo integrado en la furgoneta con múltiples emisoras sintonizables:
  - 🎷 **Salsa**
  - 🪗 **Cumbia**
  - 🔥 **Reguetón**
  - 🎸 **Baladas**
- Control de volumen independiente guardado en memoria y visualización de pista en el tablero (`RadioPlayer`, `RadioHUD`).

---

## 🎮 Controles

| Acción | Teclado & Ratón | Mando (Gamepad) |
| :--- | :--- | :--- |
| **Moverse / Conducir** | `W` `A` `S` `D` | Stick Izquierdo |
| **Mirar / Dirección de cámara** | Ratón | Stick Derecho |
| **Interactuar / Subir al auto / Abrir puertas** | `E` | Botón `X` / `Cuadrado` |
| **Coger / Soltar objeto** | `Clic Izquierdo` / `E` | `R2` / `RT` |
| **Lanzar objeto** | `Clic Derecho` | `L2` / `LT` |
| **Freno de mano** | `Espacio` | Botón `A` / `Cruz` |
| **Luces del vehículo** | `L` | `D-Pad Arriba` |
| **Radio (Encender / Estación siguiente)** | `R` / `T` | `D-Pad Derecha` |
| **Volumen de Radio (+ / -)** | `[` / `]` | `D-Pad Arriba / Abajo` |
| **Abrir / Cerrar Gran Mapa** | `M` | Botón `Select` / `Back` |
| **Pausa / Menú de Opciones** | `Escape` / `P` | Botón `Start` / `Options` |

---

## 🛠️ Estructura del Proyecto

```plaintext
A-Toda-Olla/
├── Assets/
│   ├── Audio/               # Pistas de radio clasificadas por género y efectos SFX
│   ├── Models/              # Modelos 3D de la ciudad, edificios, furgoneta y utilería
│   ├── Prefabs/             # Prefabs del jugador, vehículo de reparto, UI y objetos
│   ├── Scenes/
│   │   ├── MainMenu.unity   # Menú principal con arte estilizado y panel de opciones
│   │   └── Game.unity       # Escena principal de la ciudad y ciclo de juego
│   ├── Scripts/             # Lógica en C# dividida en módulos:
│   │   ├── Editor/          # Herramientas de automatización y setup de escenas
│   │   ├── FoodDelivery*    # Lógica de pedidos, slots y navegación
│   │   ├── Vehicle*         # Control físico de la furgoneta, taller y mejoras
│   │   ├── Shift*           # Control de jornada laboral y balance económico
│   │   ├── UI / HUD         # Controladores de interfaces, sliders, velocímetro y mapas
│   │   └── Radio*           # Sistema de audio de radioemisoras
│   └── UI/                  # Sprites, texturas, tipografías TMP y assets gráficos
├── ProjectSettings/         # Configuración del motor, layers, física y URP
└── Packages/                # Dependencias de Unity (TextMeshPro, Input System, URP)
```

---

## 🚀 Instalación y Ejecución

### Prerrequisitos
- **Unity Editor:** Versión `6000.x` (Unity 6) o superior con soporte para **Universal Render Pipeline (URP)**.
- **Git** con soporte para **Git LFS** (recomendado para modelos 3D y audio).

### Pasos para clonar y ejecutar:
1. **Clonar el repositorio:**
   ```bash
   git clone https://github.com/IsraelGarciaBetancourt/A-Toda-Olla.git
   ```
2. **Abrir con Unity Hub:**
   - Inicia Unity Hub.
   - Haz clic en **Add project from disk**.
   - Selecciona la carpeta raíz `A-Toda-Olla`.
   - Asegúrate de abrirlo con Unity 6 (`6000.x`).
3. **Cargar la escena inicial:**
   - Navega en la pestaña *Project* a `Assets/Scenes/`.
   - Abre `MainMenu.unity` para iniciar desde el menú principal, o `Game.unity` para probar directamente la jugabilidad en la ciudad.
4. **Presiona Play (▶️) en el editor.**

---

## 🎨 Menú Principal y UI

El menú principal cuenta con arte conceptual integrado y botones táctiles 3D:

<p align="center">
  <img src="Assets/UI/MainMenu/BotonJugar.png" alt="Botón Jugar" width="28%" />
  &nbsp;&nbsp;
  <img src="Assets/UI/MainMenu/BotonOpciones.png" alt="Botón Opciones" width="28%" />
  &nbsp;&nbsp;
  <img src="Assets/UI/MainMenu/BotonSalir.png" alt="Botón Salir" width="28%" />
</p>

- **Persistencia de Opciones:** Las preferencias de volumen maestro, volumen de radio vehicular y sensibilidad del ratón están conectadas mediante `PlayerPrefs` y se sincronizan tanto en el menú principal como en la pausa dentro del juego.

---

## 👨‍💻 Autor

- **Israel García Betancourt** - *Desarrollo integral, programación y diseño en Unity*
- GitHub: [@IsraelGarciaBetancourt](https://github.com/IsraelGarciaBetancourt)

---

## 📄 Licencia

Este proyecto se encuentra bajo los términos de desarrollo y derechos reservados del autor. Consulte el archivo de licencia correspondiente para más información.
