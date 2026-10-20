# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Proyecto

NavesShooter: shooter 2D de naves con **vista cenital** (top-down) para **mobile (Android/iOS)** en **orientación vertical (portrait)**. Unity **6000.6.6f1** con **URP 2D** (`com.unity.render-pipelines.universal` 17.6.0). El proyecto parte de la plantilla "Universal 2D". No es un repositorio git.

Estado actual de `Assets/`:
- `Scenes/Game.unity` — escena principal (índice 0 en Build Settings). Contiene `Main Camera` (con `CameraFollow` → `PlayerShip`), `GameBootstrap`, `Global Volume`, `Starfield`, `PlayerInput` (`PlayerInputReader`, con `GameInput` asignado), `PlayerShip` (Rigidbody2D + PolygonCollider2D + `ShipController`, hijo `Visual` con el LineRenderer), `EventSystem` (`InputSystemUIInputModule`) y el Canvas `TouchControls`.
- `Materials/` — `Starfield.mat` y `Neon.mat`, ambos `Sprite-Unlit-Default` (independientes de luces 2D).
- `Scenes/SampleScene.unity` — escena de la plantilla, todavía en Build Settings (índice 1).
- `Scripts/Core/GameBootstrap.cs` — en `Awake`: `vSyncCount = 0`, `targetFrameRate = 60`, `Screen.sleepTimeout = NeverSleep`.
- `Settings/` — assets de URP 2D (`UniversalRP.asset` con `Renderer2D.asset`, asignado en Graphics Settings y en todos los niveles de Quality), `GameVolumeProfile.asset` (perfil vacío del Global Volume, reservado para bloom), `GameInput.inputactions` (input del juego) e `InputSystem_Actions.inputactions` (de la plantilla, sin uso).
- `Welcome/` — contenido de bienvenida de la plantilla (asmdef `Unity.2D.Welcome`), no forma parte del juego.

Player Settings: plataforma activa Android; orientación fija en Portrait (`defaultScreenOrientation: 0`, autorrotación a landscape y portrait invertido deshabilitada). No cambiarla.

Cámara: ortográfica, `orthographicSize = 9.6` (1920 px / 100 PPU / 2), fondo `#05050A`, post-processing habilitado en `UniversalAdditionalCameraData`. En 1080x1920 se ven 10.8 × 19.2 unidades; con tamaño fijo, pantallas más largas (iPhone, 19.5:9) muestran el mismo alto pero menos ancho (~8.9 unidades).

## Regla de edición de assets

**Nunca editar archivos `.unity` ni `.prefab` a mano** (ni con Edit/Write ni con scripts que manipulen el YAML). Crear y modificar escenas, GameObjects, componentes y prefabs siempre con las herramientas de UnityMCP (`manage_scene`, `manage_gameobject`, `manage_components`, `manage_prefabs`, etc.).

## Estructura de scripts

Todo el código del juego va en `Assets/Scripts/`, separado por dominio:

- `Player/` — nave del jugador: movimiento, vida.
- `Input/` — lectura de input (`PlayerInputReader`), separada de quien la consume. Namespace `NavesShooter.Input`.
- `Enemies/` — comportamiento, spawn y oleadas de enemigos.
- `Weapons/` — armas, balas y patrones de disparo.
- `UI/` — HUD, menús y controles táctiles en pantalla. Namespace `NavesShooter.UI`.
- `Core/` — sistemas transversales: game manager, object pooling, utilidades compartidas.
- `Background/` — fondo de estrellas con parallax.

## Arquitectura

- **Object pooling**: balas y enemigos nunca se crean con `Instantiate`/`Destroy` en gameplay; se piden y devuelven a un pool (en `Core/`). Al reutilizar un objeto, resetear su estado (vida, velocidad, timers) en el método de activación del pool, no en `Awake`/`Start`, que solo corren una vez.
- **Fondo (`StarfieldParallax`)**: una mesh por capa generada en runtime (un quad por estrella, color de vértice). Capa con factor `parallax` p se ubica en `cámara * (1 - p)`; las estrellas que salen del área visible + `margin` se corren un múltiplo del área (wrap), y la mesh se reescribe solo en esos frames. Sorting orders -100..-98.
- **Orden de ejecución**: `StarfieldParallax` tiene `[DefaultExecutionOrder(1000)]` para que su `LateUpdate` corra después de `CameraFollow.LateUpdate`. Todo script nuevo que mueva la cámara en `LateUpdate` debe tener orden menor a 1000.
- **Nave (`ShipController`)**: se mueve siempre por `transform.up`. `MoveDirection` del reader es una dirección absoluta en pantalla (la cámara nunca rota). Fuera de `deadZone` (0.2) acelera hasta `thrustSpeed` (5) y rota hacia el ángulo de esa dirección; dentro de la zona muerta vuelve a `idleSpeed` (0.6) y sigue recta (objetivo angular 0). La velocidad angular objetivo es `min(maxTurnSpeed, √(2·angularDeceleration·Δ), Δ/dt)` para frenar justo en el ángulo; con objetivo casi opuesto (±20°) mantiene el sentido de giro en curso. `body.rotation` no se normaliza (puede pasar de ±360); comparar ángulos con `Mathf.DeltaAngle`. En `FixedUpdate` setea `angularVelocity` y `linearVelocity`, ambos con rampas vía `MoveTowards`. Rigidbody2D con `Interpolate` para que la cámara siga sin jitter. `shape` define a la vez el LineRenderer y el PolygonCollider2D (`ApplyShape`, también en menú contextual). El banking escala en X solo el hijo `Visual`, no el collider.
- **Cámara (`CameraFollow`)**: `SmoothDamp` hacia `target + target.up * lookAhead`, nunca rota. Por el lag de SmoothDamp a velocidad constante (≈ velocidad × `smoothTime`), el adelanto efectivo es menor que `lookAhead` (~1.4 de 2.5 con los valores actuales).
- **Input**: `activeInputHandler: 1` en ProjectSettings = solo Input System nuevo. `UnityEngine.Input` (legacy) lanza excepción; usar `UnityEngine.InputSystem`. `PlayerInputReader` lee el map `Gameplay` de `GameInput.inputactions` (campo `actions`) y expone `MoveDirection` (Vector2, magnitud ≤ 1) e `IsFiring`. `Move` = `<Gamepad>/leftStick` + WASD + flechas; `Fire` = `<Gamepad>/buttonSouth` + espacio. `logFiring` loguea los cambios de `IsFiring` (temporal, para verificar; sacarlo cuando haya armas). Dentro de `NavesShooter.*`, `Touch`/`TouchPhase` son ambiguos con los tipos legacy de `UnityEngine`: usar alias.
- **Controles táctiles (`TouchControls`)**: Canvas Screen Space Overlay, CanvasScaler 1080x1920 con match = 0 (ancho). Hijo `SafeArea` (`SafeAreaFitter`, anchors = `Screen.safeArea`) contiene todo. Los controles alimentan las acciones vía el gamepad virtual de `OnScreenStick`/`OnScreenButton`, no hay lectura táctil directa.
  - `JoystickZone` (cuarto inferior izquierdo del safe area): `RaycastZone` (Graphic invisible) + `FloatingJoystick`, que sigue a un solo dedo y reenvía los eventos al `OnScreenStick` de `Base/Stick` (`<Gamepad>/leftStick`, `movementRange` 100). Modo `Floating` (base salta al toque, behaviour `RelativePositionWithStaticOrigin`) o `Fixed` (solo toques sobre la base, `ExactPositionWithStaticOrigin`); cambiable con `CurrentMode` o desde el Inspector en Play. `OnScreenStick` mide en el espacio local de su padre, por eso `Stick` es hijo directo de `Base` con anchors y pivot centrados.
  - `ActionButtons` (abajo a la derecha): `VerticalLayoutGroup` con `reverseArrangement`, el primer hijo queda abajo; botones nuevos se agregan como hermanos de `FireButton` (`OnScreenButton` `<Gamepad>/buttonSouth`, ícono triángulo).
  - Visual: `NeonShapeGraphic` (círculo o triángulo, solo contorno + halo, mesh por código; hit area circular). `TouchControlFeedback` (CanvasGroup) maneja opacidad reposo/presionado (0.28/0.7) y escala (1.1). El proyecto está en **espacio de color lineal**: el blending de UI es lineal y un alpha 0.28 se ve ~57%; `perceptualAlpha` convierte con `GammaToLinearSpace` para que la opacidad configurada sea la percibida.
- **Física**: proyecto 2D — usar `Rigidbody2D`/`Collider2D` y callbacks `OnTriggerEnter2D`/`OnCollisionEnter2D`, no las variantes 3D. Vista cenital: sin gravedad (`gravityScale = 0`).
- **Render**: pipeline URP 2D Renderer; luces 2D (`Light2D`) y materiales `Sprite-Lit-Default`. No usar shaders del Built-in pipeline.
- **Mobile**: tener en cuenta resolución vertical variable y safe area (notch) en la UI y en los límites de movimiento de la nave.
- Paquetes 2D disponibles: Sprite, Tilemap (+extras), SpriteShape, 2D Animation, Aseprite y PSD importers, uGUI, Timeline, Test Framework.

## Herramientas

- **UnityMCP** (`com.coplaydev.unity-mcp`, instalado vía git en `Packages/manifest.json`): opera el Editor abierto (escenas, GameObjects, prefabs, scripts, consola, play mode, tests). Requiere el Editor abierto con el bridge MCP activo. Tras crear/editar scripts, refrescar y revisar la consola (`read_console`) por errores de compilación antes de continuar.
- **Probar en Play vía MCP**: si el Editor no tiene foco, el loop de juego no avanza; usar `Application.runInBackground = true` (solo runtime) desde `execute_code`. El input inyectado no llega al juego sin foco del Game view: cambiar temporalmente `InputSystem.settings.editorInputBehaviorInPlayMode = AllDeviceInputAlwaysGoesToGameView` y `backgroundBehavior = IgnoreFocus` (settings en memoria, no persisten) y restaurar al terminar. Mouse y teclado reales (o resets por foco) pisan estados inyectados: usar dispositivos virtuales (`AddDevice<Keyboard>("TestKeyboard")`, que pasa a ser `Keyboard.current` al recibir eventos); para toques, `AddDevice<Touchscreen>` virtual + `QueueStateEvent` con `TouchState` (posiciones en píxeles del Game view; los controles pasan por el EventSystem, así que se prueban de punta a punta), y removerlo después. `execute_code` corre en contexto editor: leer valores del juego (p. ej. `PlayerInputReader.MoveDirection`), no el estado de dispositivos. Su compilador puede ser CodeDom (C# 6): sin `out var` ni funciones locales, y `Object` es ambiguo (usar `UnityEngine.Object`).
- Plugin `unity@unity-agent-plugin` habilitado en `.claude/settings.local.json` (skills `unity:*`).

## Comandos

No hay scripts de build ni CI. Desde línea de comandos se usa el ejecutable del Editor (ruta típica en Windows: `C:\Program Files\Unity\Hub\Editor\6000.6.6f1\Editor\Unity.exe`). Batchmode falla si el proyecto ya está abierto en otro Editor.

```powershell
# Tests (Unity Test Framework). EditMode o PlayMode
& $unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml -logFile -

# Un solo test o fixture
& $unity -batchmode -projectPath . -runTests -testPlatform EditMode -testFilter "Namespace.Clase.Metodo" -testResults TestResults.xml -logFile -
```

Con el Editor abierto, preferir la herramienta `run_tests` de UnityMCP (admite filtro por nombre). Los tests requieren un asmdef de tests que referencie los asmdef del juego; todavía no existe ninguno.
