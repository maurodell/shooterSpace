# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Proyecto

NavesShooter: shooter 2D de naves con **vista cenital** (top-down) para **mobile (Android/iOS)** en **orientación vertical (portrait)**. Unity **6000.6.6f1** con **URP 2D** (`com.unity.render-pipelines.universal` 17.6.0). El proyecto parte de la plantilla "Universal 2D". No es un repositorio git.

Estado actual de `Assets/`:
- `Scenes/SampleScene.unity` — única escena en Build Settings.
- `Settings/` — assets de URP 2D (renderer, pipeline asset, volume profiles), `Scenes/URP2DSceneTemplate.unity` e `InputSystem_Actions.inputactions`.
- `Welcome/` — contenido de bienvenida de la plantilla (asmdef `Unity.2D.Welcome`), no forma parte del juego.

Pendiente en Player Settings: `defaultScreenOrientation` está en AutoRotation (4) con rotación a landscape permitida; debe quedar fijo en Portrait.

## Regla de edición de assets

**Nunca editar archivos `.unity` ni `.prefab` a mano** (ni con Edit/Write ni con scripts que manipulen el YAML). Crear y modificar escenas, GameObjects, componentes y prefabs siempre con las herramientas de UnityMCP (`manage_scene`, `manage_gameobject`, `manage_components`, `manage_prefabs`, etc.).

## Estructura de scripts

Todo el código del juego va en `Assets/Scripts/`, separado por dominio:

- `Player/` — nave del jugador: movimiento, vida, lectura de input.
- `Enemies/` — comportamiento, spawn y oleadas de enemigos.
- `Weapons/` — armas, balas y patrones de disparo.
- `UI/` — HUD, menús y controles táctiles en pantalla.
- `Core/` — sistemas transversales: game manager, object pooling, utilidades compartidas.

## Arquitectura

- **Object pooling**: balas y enemigos nunca se crean con `Instantiate`/`Destroy` en gameplay; se piden y devuelven a un pool (en `Core/`). Al reutilizar un objeto, resetear su estado (vida, velocidad, timers) en el método de activación del pool, no en `Awake`/`Start`, que solo corren una vez.
- **Input**: `activeInputHandler: 1` en ProjectSettings = solo Input System nuevo. `UnityEngine.Input` (legacy) lanza excepción; usar `UnityEngine.InputSystem`. Los controles son **táctiles**; el asset `InputSystem_Actions` ya define el action map `Player` (Move, Look, Attack, Interact, Crouch, Jump, Previous, Next, Sprint) y un map `UI`, con bindings de teclado/gamepad útiles para probar en el Editor.
- **Física**: proyecto 2D — usar `Rigidbody2D`/`Collider2D` y callbacks `OnTriggerEnter2D`/`OnCollisionEnter2D`, no las variantes 3D. Vista cenital: sin gravedad (`gravityScale = 0`).
- **Render**: pipeline URP 2D Renderer; luces 2D (`Light2D`) y materiales `Sprite-Lit-Default`. No usar shaders del Built-in pipeline.
- **Mobile**: tener en cuenta resolución vertical variable y safe area (notch) en la UI y en los límites de movimiento de la nave.
- Paquetes 2D disponibles: Sprite, Tilemap (+extras), SpriteShape, 2D Animation, Aseprite y PSD importers, uGUI, Timeline, Test Framework.

## Herramientas

- **UnityMCP** (`com.coplaydev.unity-mcp`, instalado vía git en `Packages/manifest.json`): opera el Editor abierto (escenas, GameObjects, prefabs, scripts, consola, play mode, tests). Requiere el Editor abierto con el bridge MCP activo. Tras crear/editar scripts, refrescar y revisar la consola (`read_console`) por errores de compilación antes de continuar.
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
