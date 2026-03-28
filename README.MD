# 🎮 Project Name: WEIGHT of the WORLD


## Setup (Do this once)
1. **GitHub Desktop:** Download and sign in to the [GitHub Desktop App](https://desktop.github.com).
2. **Clone:** In the app, go to `File > Clone Repository` and select this project.
3. **Unity Hub:** Open Unity Hub, click **Add**, and select the folder you just cloned.
4. **LFS Check:** Ensure Git LFS (Large File Storage) is installed if prompted, so our 3D models and textures sync correctly.

---

## Workflow
To avoid "Merge Conflicts" (where Git gets confused by our changes), always follow these steps:

### 1. Before you start (Sync up)
*   Open **GitHub Desktop**.
*   Click **Fetch origin**. If there are updates, click **Pull origin**.
*   *This ensures you aren't working on an old version of the game.*

### 2. The Sandbox (Branching)
*   **Never work directly on the `main` branch.**
*   Click **Current Branch** > **New Branch**. 
*   Name it based on what you are doing (e.g., `feat-player-movement` or `fix-lighting-bugs`).
*   Click **Publish Branch**.

### 3. Making Changes (Unity)
*   Open the project in Unity.
*   **The Golden Rule:** Only one person should edit a specific `.unity` Scene file at a time. If we both move objects in "Level_01" at once, Git will break!
*   Save your Scene (`Ctrl+S`) and Project often.

### 4. Saving & Uploading (Commit & Push)
*   When you're done for the day, go back to **GitHub Desktop**.
*   On the bottom left, write a summary of what you did (e.g., "Adjusted gravity").
*   Click **Commit to [Branch Name]**.
*   Click **Push origin** at the top. This uploads your work to the website.

### 5. The "Merge" (Pull Request)
*   Once your feature is 100% done, click **Create Pull Request** in GitHub Desktop.
*   This opens the GitHub website. Click the green **Create Pull Request** button.
*   We can both review it, then click **Merge Pull Request** to officially add it to the `main`

---

## Emergency Tips
*   **Library Folder:** If you see 5,000+ files waiting to be "Committed," **STOP**. Your `.gitignore` isn't working.
*   **Locked Files:** If GitHub Desktop says it can't move a file, close the Unity Editor and try again.
