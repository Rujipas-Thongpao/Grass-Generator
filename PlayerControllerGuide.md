# PlayerController for Vertex-Displaced Terrain

## Overview

This document describes the `PlayerController.cs` script, which enables a first-person player character to move on terrain whose visual geometry is displaced by a vertex shader using a height map (RenderTexture).

## Features

*   **First-Person View:** Mouse-controlled camera for looking (pitch and yaw).
*   **Character Movement:** Standard WASD keyboard input for movement.
*   **Jumping:** Ability to jump, with configurable height.
*   **Dynamic Terrain Following:** The player's height adjusts to stay on ground that is visually displaced using a `RenderTexture` (referred to as `heightRT`).
*   **CPU-Side Height Sampling:** The script reads from the `heightRT` on the CPU to determine the terrain height at the player's position.
*   **Inspector Configuration:** Many parameters are exposed in the Unity Inspector for easy tuning.
*   **Fallback Movement:** If `heightRT` is not assigned, the controller falls back to standard `CharacterController` physics for ground detection and movement (though gravity is always explicitly handled by the script).

## How to Use

1.  **Attach Script:** Attach the `PlayerController.cs` script to your player GameObject in Unity. This GameObject should typically represent your player character.
2.  **CharacterController Component:** The script uses `[RequireComponent(typeof(CharacterController))]`, so a `CharacterController` component will be automatically added to the GameObject if it doesn't already have one.
    *   Adjust the `Center`, `Radius`, `Height`, `Step Offset`, and `Slope Limit` of the `CharacterController` in the Inspector to fit your player model and desired movement characteristics.
3.  **Configure PlayerController Parameters in Inspector:**
    *   **`Mouse Sensitivity`**: Controls how fast the camera rotates with mouse movement (e.g., `100f`).
    *   **`Player Camera`**: Assign the Transform of your main FPS camera (usually a child of the player GameObject, or the main scene camera if it's dedicated to this player). If not assigned, it will try to find `Camera.main`.
    *   **`Speed`**: Movement speed of the player (e.g., `5f` to `12f`).
    *   **`Gravity`**: Strength of gravity. A value like `-19.62f` (which is `-9.81f * 2`) provides a slightly more responsive feel than realistic gravity (e.g., `-9.81f`).
    *   **`Jump Height`**: The desired height the player can jump (e.g., `1.5f` to `3f`).
    *   ---
    *   **`Height RT` (RenderTexture)**: **Crucial for displaced terrain.** Assign the `RenderTexture` asset that your vertex shader uses as a height map. The script will sample this to determine ground height.
    *   **`Terrain World Size` (Vector3)**:
        *   `X`: The width of your terrain in world units that corresponds to the full width of the `heightRT`.
        *   `Y`: The maximum height displacement in world units. This value is used to scale the normalized height value read from `heightRT` (assumed to be in the R channel, 0-1 range).
        *   `Z`: The depth (or length) of your terrain in world units that corresponds to the full height of the `heightRT`.
    *   **`Terrain Origin` (Vector3)**: **Crucial for correct sampling.** This is the world-space coordinate `(X, Y, Z)` of the corner of your terrain that corresponds to the UV coordinate (0,0) on your `heightRT`. For example, if your terrain mesh plane starts at `(0,0,0)` and extends into positive X and Z, then `terrainOrigin` would be `(0,0,0)`. If it's centered at world origin and is 1000x1000 units, `terrainOrigin` might be `(-500, 0, -500)`.
    *   **`Height RT Solution` (Vector2Int)**: The resolution (width and height in pixels) of your `heightRT` (e.g., X: 1024, Y: 1024).

4.  **Player Object Setup:**
    *   Ensure the player GameObject (with the `PlayerController` script) is positioned at a reasonable starting point above your terrain.
    *   If your FPS camera is a child of the player GameObject, position it appropriately for the first-person view (e.g., at eye level).

5.  **Ground/Terrain Setup:**
    *   Your terrain that uses `heightRT` for vertex displacement should be present in the scene.
    *   The `PlayerController` does *not* require this terrain to have a conventional collider that matches the displacement for height adjustment *if `heightRT` is assigned and configured correctly*. It samples the `heightRT` directly.
    *   However, you might still want basic colliders on other objects or a low-resolution base collider for the terrain for other physics interactions or if `heightRT` is disabled.

## Script Logic Overview

*   **Camera Control (`Update`)**: Rotates the player body (yaw) and the camera (pitch) based on mouse input.
*   **Movement Input (`Update`)**: Reads WASD input to create a movement direction vector.
*   **Height Sampling Control (`Update` & `InitializeHeightSampling`)**:
    *   Checks if `heightRT` is assigned. If so, `useSampledHeight` is true.
    *   `InitializeHeightSampling` prepares a temporary 1x1 `Texture2D` (`tempHeightTexture`) with a readable format (`RGBA32`) to receive pixel data from `heightRT`.
*   **Terrain Height Calculation (`GetTerrainHeightFromRT`)**:
    *   Converts the player's world X/Z position to UV coordinates based on `terrainOrigin` and `terrainWorldSize`.
    *   Clamps UVs and converts them to pixel coordinates for `heightRTSolution`.
    *   Uses `Texture2D.ReadPixels()` to copy the pixel from `heightRT` at the calculated coordinates into `tempHeightTexture`. **Note: `ReadPixels()` is a synchronous CPU operation and can be slow.**
    *   Reads the R channel of the color from `tempHeightTexture` and scales it by `terrainWorldSize.y` to get the world height.
*   **Movement Application (`Update`)**:
    *   **If `useSampledHeight` is true**:
        1.  Applies horizontal movement.
        2.  Calls `GetTerrainHeightFromRT` to get the target ground height.
        3.  Calculates `heightError` (difference between target height and player's current feet position).
        4.  Applies a vertical move to correct the player's height, snapping them to the terrain.
        5.  `isGrounded` is determined by checking if `abs(heightError)` is below a small threshold (e.g., `0.1f`).
        6.  If `isGrounded`, a small downward velocity (`-2f`) is set to help stick to the ground. Otherwise, gravity is applied.
    *   **If `useSampledHeight` is false**:
        1.  Applies horizontal movement.
        2.  Uses `CharacterController.isGrounded` for ground detection.
        3.  Applies gravity if not grounded or sets sticking velocity if grounded.
*   **Jumping (`Update`)**: If `isGrounded` and jump button is pressed, applies an upward velocity.
*   **Gravity (`Update`)**: Accumulates downward velocity due to gravity, which is applied in the final `controller.Move()` call.
*   **Resource Cleanup (`OnDestroy`)**: Destroys `tempHeightTexture` to prevent memory leaks.

## Possible Improvements & Future Work

1.  **Asynchronous GPU Readback (`AsyncGPUReadback`)**:
    *   The current `Texture2D.ReadPixels()` is synchronous and can significantly impact performance, especially if called frequently or with high-resolution `heightRT`s (though this script only reads 1x1 pixel regions).
    *   Replace `ReadPixels` with `AsyncGPUReadback.Request()` for non-blocking height data retrieval from the GPU. This is more complex to implement as it involves handling requests and callbacks, but it's the standard way to optimize this.

2.  **Height Interpolation/Smoothing**:
    *   The current height adjustment snaps the player directly to the sampled height. This can feel abrupt or cause jitter if the `heightRT` data is noisy or if player movement between sample points is significant.
    *   Implement smoothing by interpolating the player's height towards the target height over a short duration (e.g., using `Mathf.Lerp` or `Mathf.SmoothDamp`). This often requires careful tuning to avoid a "floaty" feel or lag in response.

3.  **Multi-Point Sampling / Slope Detection**:
    *   Currently, only one point directly beneath the player is sampled. For more robust interaction with slopes:
        *   Sample multiple points around the player (e.g., under each corner of a small footprint).
        *   Calculate an average height or derive a normal vector for the surface.
        *   Align the player to this surface normal for more natural footing on slopes. This is considerably more complex.

4.  **Error Handling for `heightRT` Formats**:
    *   While `TextureFormat.RGBA32` is used for `tempHeightTexture`, `ReadPixels` might still fail or return unexpected results if the `heightRT` itself has a very unusual or non-color format (e.g., certain depth stencil formats not intended for direct color reading). More specific checks or handling for `heightRT.format` could be added if issues arise.

5.  **Configurable Height Channel**:
    *   Currently, height is hardcoded to be read from the `R` (red) channel of `heightRT`. This could be made configurable (e.g., via an enum for R, G, B, A).

6.  **Physics Interactions with Displaced Terrain**:
    *   This controller primarily handles visual alignment with the displaced terrain. For other physics objects to interact correctly with the displaced surface, the actual mesh colliders would need to be updated, or a separate system for "virtual" physics queries against the `heightRT` would be needed. This is a much larger task.

7.  **Network Synchronization**:
    *   For multiplayer games, synchronizing this type of custom controller, especially one that relies on reading GPU data, requires careful design to ensure consistent state across clients.

8.  **Editor Gizmos**:
    *   Add `OnDrawGizmos` to display the `terrainOrigin`, `terrainWorldSize`, or the point being sampled, which can help with debugging setup issues in the editor.
