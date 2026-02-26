# LOTUSim-Energy

LOTUSim-Energy leverages the core simulation framework (https://github.com/naval-group/LOTUSim) and physics interface (https://github.com/naval-group/LOTUSim-Xdyn) provided by these repositories.

<div style="display: flex; gap: 10px; align-items: flex-start;">
  <img src="Media/wind_farm.png" alt="wind_farm" width="35%">
  <img src="Media/bluerov_lrauv.png" alt="bluerov_lrauv" width="44%">
</div>

## Overview

LOTUSim-Energy is a distributed, server–client simulation framework designed for multi-domain robotics. Gazebo serves as the central orchestrator for asset management and simulation timing (deterministic step scheduler), while separate client modules execute specific simulation tasks.

The core simulation control module interfaces with three primary client modules:

- Physics: LOTUSim-Xdyn as the physics interface
- Agent Interaction: ROS 2 for inter-agent messaging and hardware-in-the-loop bridges
- Rendering: Unity as the optional high-fidelity renderer for human–robot interaction (HRI)

<img src="Media/lotusim_architecture.png" alt="architecture" width="30%">

## Table of Contents
- [Requirements / Setup](#requirements--setup)
- [Get Started](#get-started)
- [Start using the Leap Motion](#start-using-the-leap-motion)
- [Start using the Tobbi Eye Tracker 5](#start-using-the-tobbi-eye-tracker-5)
- [Packages / Assets available](#packages--assets-available)
- [Scenes](#scenes)
- [Scripts](#scripts)
- [Executables](#executables)
- [Appendix](#appendix)


## Requirements / Setup
- **Unity** `2022.3.18f`  
- **Multi-Users** `PUN2 - Photon Unity Networking 2` (already in the git) 
- **Leap Motion** (works on Linux and Windows) An `Ultraleap Hand Tracking Camera` 
You will need a computer that meets the [Tracking Requirements](https://www.ultraleap.com/gemini-downloads/?_gl=1*1p21y34*_ga*MjA3MTg2NzM3NS4xNzQ1ODk1Mjky*_ga_5G8B19JLWG*czE3NTAzOTQ1ODQkbzEwJGcwJHQxNzUwMzk0NTg0JGo2MCRsMCRoMA..) and have the `[Ultraleap Hand Tracking Software (V5.2+)]`(https://www.ultraleap.com/downloads/) installed (for this project, the **Leap Motion Controller** have been used) 
- **Eye Tracker (only on Windows)** `Tobii Eye Tracker 5` device  
Tobii Gaming | Download or Setup Eye Tracking Software and Drivers install the driver
Then Tobii Ghost,
Install the `Tobii Experience App` in the Microsoft Store  
The `Tobii Experience Driver v1.133` (https://gaming.tobii.com/getstarted/?bundle=tobii-et5)  
And the `Tobii Ghost v1.14.1` (https://gaming.tobii.com/getstarted/?bundle=tobii-et5)

---

## Get Started

1. **Get the Unity project**    
Use this command :
```bash
git clone --recurse-submodules https://github.com/naval-group/LOTUSim-Unity-modules
```

And after cloning:
```sh
cd LOTUSim-Unity-modules
git submodule update --remote --merge
```

2. **Add & open the project on Unity Hub.**
3. **Open one of the scenes**
4. **Enjoy !  🎉**   

## Start using the Leap Motion

1. Make sure you have the [requirements](#requirements/setup) installed for the Leap Motion with the device you have.
2. Plug your Leap Motion and check by using the Leap Motion Software if it is active on your computer.
3. Place the Leap in front of the user, with the **wire pointing left** (the Leap has been implemented to be Desktop mounted)
4. Check that the Unity scene you are using have the GameObject `LeapMouvementController` activated (in Unity).
> Note: in this repo, only the `defenseScenario` scene have been developped to work with the Leap Motion.
5. If you see **red lights** on the cameras of the Leap, it is ready to be used!

## Start using the Tobbi Eye Tracker 5

1. Plug the Eye Tracker, and make sure you have the [requirements](#requirements/setup) installed.
2. Open the `Tobii Experience App` and callibrate your device by following the instructions.
3. Open the `Tobii Ghost` app and play with the settings to display the gaze trace and more...
4. You can start running your simulations and the Eye Tracker will track your gaze!

---

## Packages / Assets available
- `CityPeople`: Unity asset available [here](https://assetstore.unity.com/packages/3d/characters/city-people-free-samples-260446) to have civilians models.
- `HandPoses`: Recorded hand poses to move in the `defenseScenario` scene with hands gestures instead of using the keyboard for a more natural interaction.
- `IslandTools`: Unity tools to build the environment.
- `LowPolySolider_demo`: Unity asset available [here](https://assetstore.unity.com/packages/3d/characters/low-poly-soldiers-demo-73611) to have soldiers models. 
- `Photon`: Photon Unity Networking ([PUN](https://doc.photonengine.com/pun/current/getting-started/pun-intro)) is a Unity package for multiplayer games. Flexible matchmaking gets your players into rooms where objects can be synced over the network.

---

## Scenes

### Unity Scenes Folder Overview

The Unity project includes three main scene folders, each designed to support specific simulation and interaction features within the LOTUSim environment.


### LeapMotion
The **LeapMotion** folder includes scenes and assets used to **record and interpret hand poses** for controlling camera movement.  
This provides a **more natural and immersive interaction**, replacing traditional keyboard controls.  
To use it, simply **launch the scene** and **follow the on-screen instructions** to calibrate and test hand-based input.

### MultiUser
The **MultiUser** folder contains all scenes related to **multi-user functionality**.  
It includes a **launcher scene** that allows users to **spawn a customizable Kyle robot** (editable via the *Kyle* scene) and enter a **shared two-player environment**.  
This setup enables synchronized interactions and collaborative experiments, and it is **already integrated into certain scenes**, such as the **Defense** scenario.

---

## Scripts

The project is organized into several script folders, each handling a specific aspect of the simulation and interaction system.
The `Camera` folder contains scripts for managing and switching between different camera views across scenes.
The folder `EditorEnvironment` manages real-time simulation parameters, including the display of the Real-Time Factor, computation of the FPS, and interactive controls like the wind slider. It also includes scripts that control environmental elements such as the sun and clouds.
The `LeapMotion` folder handles hand-tracking interactions.
The `MultiUser` folder enables multi-user connectivity and synchronization.
Finally, the `XR-Controller` folder provides VR support, managing user input and interactions in immersive environments.


#### Camera:
- `CameraDynamicTargetsNavigator.cs` : Dynamically navigates the camera between scene objects by smoothly moving, rotating, and zooming. Supports arrow key navigation (← →) to cycle through targets.
- `CameraKnownTargetsNavigator.cs` :  Handles smooth navigation between a predefined list of known scene targets.
    - Moves the camera smoothly toward each target using configurable offsets.
    - Performs smooth rotation to face each target.
    - Smoothly adjusts the camera's field of view (zoom) per target.
    - Navigation between targets is controlled using the Left and Right arrow keys.
- `CameraManager.cs` :  Manages switching between multiple sets of Cinemachine virtual cameras in a scene.
    - Each set (Front, Right, Left) represents a different viewpoint of the same scene target.
     - Uses arrow key input to cycle through targets.
- `CameraModeSwitcher.cs` : Switches between automatic target-following camera mode and free-fly spectator mode.
     - Arrow keys (↑ ↓ ← →) enable Auto mode (entity navigation).
     - Q, W, E, A, S, D keys enable Free-Fly mode (manual movement).
- `CameraSensor.cs`:  Publishes RGB camera frames from Unity to ROS via the ROS–TCP Connector. Designed for integration with Unity Robotics Hub and Lotusim's simulation framework.
- `DynamicObjectNameDisplay.cs` : Dynamically displays the names of objects above them in the scene. Allows the user to toggle the visibility of these labels using a designated key.
- `FPSLimiter.cs` : Controls the application's target frame rate to ensure consistent performance.
-`FreeFlyCamera.cs` : Controls the camera for a free fly spectator. Camera movement by 'W','A','S','D','Q','E' and speed of the quick camera movement when holding the 'Left Shift' key.

#### MultiUser:
- `CameraWork.cs` : Used to deal with the Camera work to follow the player : Used to deal with the Camera work to follow the player  
- `GameManager.cs` : Used to handle the game management so, instanciating players and cameras depending which user is being used  
- `Launcher.cs` : Used to connect, and join/create room automatically on player or spectator mode  
- `PlayerAnimatorManager.cs` : Used to deal with the networked player Animator Component controls.  
- `PlayerManager.cs` : Used to deal with the networked player instance  
- `PlayerNameInputField.cs` : Let the player input his name to be saved as the network player Name, viewed by alls players above each when in the same room.   
- `PlayerUI.cs` : Used to deal with the networked player instance UI display tha follows a given player to show its health and name  
- `SpectatorCamera.cs` : Used to set the spectator camera movements  


### EditorEnvironment:
- `AgentSpawner.cs` : Spawns multiple instances of a given model prefab at defined positions, optionally using CLI arguments.
- `common.cs` : Utility for converting ROS/Gazebo coordinate system (right-handed) to Unity (left-handed) coordinate system.
- `EnvironmentControlEditor.cs` : Scripts to control the sun and the rain in a scene.
- `EnvironmentController.cs` : Scripts to control the sun and the rain in a scene.
- `FpsTracker.cs`: Tracks FPS over the last 1000 frames and saves the results to a CSV file on application quit.
- `InfiniteSeabed.cs` : Manages an infinite seabed using a 3x3 grid of tiles around the camera.
- `InputLockManager.cs` :  Handles cursor locking/unlocking and enables or disables camera control accordingly. Useful for toggling between gameplay (mouse locked) and UI interaction (mouse free).
- `LotusimConnectorEditor.cs` : Custom Unity Editor script for LotusimInterface that lets users select and update the interface type and namespace directly in the Inspector, automatically applying changes and triggering relevant callbacks.
- `RTFLabelUpdate.cs` :  Displays the Real-Time Factor (RTF) from the ROS simulation as a percentage on a TMP label. Toggles visibility with the 'L' key.
- `WindSliderController.cs` : Controls wind vector sliders along X, Y, Z axes and publishes their values to a ROS2 topic via TCP/IP.
 Supports keyboard shortcuts for increment/decrement and reset.


### LeapMotion:
- `LeapMotionMovement.cs` :  Uses Leap Motion hand pose detection to control a CharacterController in 3D space. Supports movement in six directions: forward, back, left, right, up, and down.


### lotusim_interface:
- `commons.cs` : Utility class for converting poses between Gazebo and Unity coordinate systems.
- `InterfaceFactory.cs` : Factory and driver for creating and updating Lotusim interfaces (ROS2, TCPIP, etc.)
- `LotusimBaseInterface.cs` : Base abstract class for all interfaces in the Lotusim system. Interfaces populate pose, creation, destruction, and propeller data for vessels. 
- `LotusimConnector.cs` : Main Unity interface for Lotusim. Wraps LotusimBaseInterface implementations (ROS2, TCPIP). Handles creation, destruction, and updating of vessels, transforms, and animations.
- `ROSConnectionConfigurator.cs` : Reads ROS IP and port from PlayerPrefs and configures the ROSConnection singleton.


### lotusim_interface/ROS2_interface:
- `RosInterface.cs` :  Singleton interface for ROS2 communication in Lotusim. Handles vessel pose updates, renderer commands, dynamic vessel commands, and simulation stats.


### lotusim_interface/Tcp_interface:
- `TcpIpInterface.cs` :  Handles UDP/TCP communication with external clients for vessel updates and commands. Supports thread-safe data reception and processing, including vessel positions and commands.
- `TCPIPInterfaceTypes.cs` : Contains data structures and JSON converters for vessel info and Unity commands used in TCP/IP communication in Lotusim.


### xr_controller
- `XR Controller.cs` : Handles movement of the XR camera in 3D space based on directional commands.



---


## Executables

Executable for _Linux_ and _Windows_ of **LOTUSim** are available in the `lotusim-generic-scenario` repository.
It includes the **DefenseScenario** and **Launcher** scenes already built and ready to run.  
All build details and usage instructions are thoroughly explained in the **README** and **Wiki** of the `lotusim-generic-scenario` section and repository.

>**Note:** If you wish to develop your own scenario or Unity scene, you can integrate it with LOTUSim-core (repo LOTUSim)using the same `lotusim-generic-scenario` framework.  
Before doing so, make sure to **build your Unity scene** for the desired platform and follow the same **linking process** described in the `lotusim-generic-scenario` documentation to connect Unity with LOTUSim.


## Appendix

### Hydrodynamic Parameters

Hydrodynamic parameters by agent (LRAUV values from the OSRF LRAUV Tethys model):

| **Param** / **Agent** | **LRAUV** | **BlueROV** |
| ------------------ | --------- | ----------- |
| **Mass $m$ [kg]**  | 147.87    | 10          |
| -------------------- | --------- | ----------- |
| **Added mass**       | **LRAUV** | **BlueROV** |
| $X_{\dot u}$ [kg]    | -4.8762   | 0           |
| $Y_{\dot v}$ [kg]    | -126.3247 | 0           |
| $Z_{\dot w}$ [kg]    | -126.3247 | 0           |
| $M_{\dot q}$ [kg·m²] | -33.4631  | 0           |
| $N_{\dot r}$ [kg·m²] | -33.4931  | 0           |
| $M_{\dot w}$ [kg·m]  | +7.1178   | 0           |
| $N_{\dot v}$ [kg·m]  | -7.1178   | 0           |
| $Y_{\dot r}$ [kg·m]  | -7.1178   | 0           |
| $Z_{\dot q}$ [kg·m]  | +7.1178   | 0           |
| **Sensors** | Sparton AHRS-M2 Magnetometer + IMU + Additional sensors (see Table `lotusim_tasks`) | IMU + Additional sensors (see Table `lotusim_tasks`) |
| ------------------------------- | --------- | ----------- |
| **Linear damping coefficients** | **LRAUV** | **BlueROV** |
| $X_u$ [N·s·m⁻¹]                 | 0         | 11.7391     |
| $Y_v$ [N·s·m⁻¹]                 | 0         | 20          |
| $Z_w$ [N·s·m⁻¹]                 | 0         | 31.87       |
| $K_p$ [N·m·s]                   | 0         | 25          |
| $M_q$ [N·m·s]                   | 0         | 44.91       |
| $N_r$ [N·m·s]                   | 0         | 5           |
| ------------------- | ------- | ------- |
| **Quadratic damping coefficients** | **LRAUV** | **BlueROV**  |
| $X_{\|u\|u}$ [kg·m⁻¹] | —       | 0       |
| $Y_{\|v\|v}$ [kg·m⁻¹] | -601.27 | 0       |
| $Z_{\|w\|w}$ [kg·m⁻¹] | -601.27 | 0       |
| $K_{\|p\|p}$ [kg·m²]  | -0.19   | 0       |
| $M_{\|q\|q}$ [kg·m²]  | -632.7  | 0       |
| $N_{\|r\|r}$ [kg·m²]  | -632.7  | 0       |
