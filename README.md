# 🎮 JUANES – Adaptive Gamepad Interface

<p align="center">
  <img src="git_gif.gif" width="700"/>
</p>

> Adaptive interface system designed to support early-stage gamepad mastery without modifying game difficulty.

---

## 🧠 Overview

**JUANES** is a Unity-based experimental FPS environment developed as an academic thesis project.
It proposes a novel approach to player onboarding by introducing an **adaptive interface layer** that provides real-time feedback to improve **gamepad control skills**, rather than altering game difficulty.

The system focuses on supporting users with **low experience using controllers**, helping them overcome the initial learning barrier through contextual, perceptual feedback.

---

## 🎯 Key Features

* 🎮 FPS-based experimental environment (Unity)
* 🧠 Adaptive interface driven by real-time player metrics
* 📊 Telemetry system for behavioral analysis (CSV logging)
* 🔄 Dynamic feedback (visual, audio, contextual)
* 🎯 Focus on **motor skill acquisition**, not difficulty scaling

---

## 🧩 System Architecture (Pillars)

The system is structured into multiple adaptive pillars:

* **Orientation** → Camera control and alignment
* **Trajectory** → Movement planning and execution
* **Landing** → Precision in spatial positioning
* **Movement** → Navigation efficiency
* **Obstacles** → Reactive control under constraints
* **Multitasking** → Simultaneous input demands
* **Jitter** → Fine motor stability and control noise

Each pillar collects metrics and activates contextual feedback when necessary.

---

## 🧪 Evaluation

The system was evaluated through a controlled experimental design:

* 👥 Participants: users with low gamepad experience
* ⚖️ Conditions:

  * Base (no adaptive interface)
  * Adaptive (JUANES system active)
* 📈 Metrics:

  * Performance (time, deaths, progression)
  * Behavioral indicators (e.g., jitter, control stability)
* 📉 Analysis:

  * Non-parametric tests (Mann–Whitney U)
  * Distribution comparison (medians, boxplots)

Results suggest improvements in **learning progression and control stability** under the adaptive condition.

---

## 🖥️ Installation & Usage (Unity)

1. Clone the repository:

```bash
git clone https://github.com/juanfraaa3/game-fps-adaptive-interface.git
```

2. Open the project in Unity
   *(recommended version: **2022.3.62f1**)*

3. Load the main scene:

```
Assets/Scenes/MainScene.unity
```

4. Press ▶️ Play

---

## ⚠️ IMPORTANT – Hardcoded Paths (Read Before Running)

This project contains **hardcoded file paths** in several scripts (mainly related to CSV logging and data output).

👉 Before running the project, you MUST update these paths to match your local machine.

### 🔧 How to fix it

1. Open the project in Unity or your code editor
2. Press:

```
Ctrl + Shift + F
```

3. Search for patterns like:

```
C:/Users/
```

4. Replace them with:

* Your own local paths
* Or a relative path if preferred

---

### ⚠️ If you skip this step:

* CSV logs may not be generated
* Some systems may fail silently
* Metrics collection may break

---

## 🎮 Controls

* Left Stick → Movement
* Right Stick → Camera
* Triggers → Actions / Shooting
* Buttons → Contextual interactions

*(Designed specifically for gamepad usage)*

---

## 📂 Project Structure

```
Assets/
 ├── Scripts/
 ├── Scenes/
 ├── Materials/
 ├── Systems/
 └── UI/

ProjectSettings/
Packages/
```

---

## 🎥 Media

Gameplay preview shown above.

---

## 📄 Thesis

This project was developed as an undergraduate thesis:

**"Desarrollo y evaluación de una interfaz adaptativa basada en métricas para el dominio inicial del gamepad"**

📥 Full thesis available here:
https://drive.google.com/file/d/1xO9N5NS0xLKEcNVwpJr4Yze_M0emx960/view?usp=sharing

---

## 🧠 Technologies

* Unity Engine (2022.3.62f1)
* C#
* CSV logging system

---

## 🌍 Impact

JUANES contributes to:

* 🎮 Game accessibility
* 🧠 UX in interactive systems
* 🎯 Learning support for non-expert players

---

## 📌 Future Work

* Removal of hardcoded paths (full portability)
* Improved data pipeline integration
* Extension to other game genres

---

## 👤 Author

**Juan Francisco Baquedano Belmar**
📧 [juan.baquedano@usm.cl](mailto:juan.baquedano@usm.cl)

---

## 📜 License

This project is for academic and research purposes.

---
