## Current Unity project

Open this repository in Unity 6000.3.10f1 (Unity 6.3 LTS).

Clone with Git LFS installed, then run `git lfs pull` to download models and large scenes. Open the project through Unity Hub and allow Unity to import the assets. The playable scenes are under `Assets/Scenes` (LevelSelect, Level1, Level2, Level3).

The latest three-level diving demo includes the shared cave terrain, underwater movement, breathing audio, flashlight, and rescue mission. Heart-rate connection gates, heart-rate failure rules, and the heart-rate overlay are currently disabled.

HypeRate credentials are intentionally blank in this repository. To use that integration later, supply your own API key and device ID locally in `Assets/Resources/HeartRate/Connection.json`; do not commit credentials.

Unity caches, generated IDE files, local editor settings, and temporary verification artifacts are excluded. Audio source credits are included with the audio assets.
