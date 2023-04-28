# D4HUD

Experimental project for an overlay in Diablo IV.
Allows you to place skills/cooldowns at a more convenient location.

## Configuration

Configuration uses mouse position and delays to interact with the overlay menu without interfering with Diablo IV.
Holding the the CTRL modifier key allows you to drag the ROIs and interface items around.

<img src="./readme/readme-01.png" width="500">

<img src="./readme/readme-02.png" width="500">

## Experimental

Performance is currently bad though. I capture the ROIs of each skill first and then draw them again at the preferred location.
An approach using mem reading would be much better. Then you could draw the skill/cooldown info directly.
