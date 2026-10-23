# Tunables

Generated from `@tune` headers by the menu `CGDD/Export Trace Docs`. Hand edits are overwritten on the next export.

| Script | Field | Type | Range | Default | Note |
| --- | --- | --- | --- | --- | --- |
| CameraFollow | smoothSpeed | float | 1-20 | 5 | Follow smoothing |
| ChickenBuilder | size | float | 0.5-30 | 1 | Chicken size |
| ChickenBuilder | outlineWidth | float | 0-0.05 | 0.012 | Outline thickness |
| ClimberBuilder | size | float | 0.5-3 | 1 | Corpse size |
| ClimberBuilder | outlineWidth | float | 0-0.05 | 0.012 | Outline thickness |
| CrashTransition | slowMotion | float | 0.05-1 | 0.15 | Time scale right after the crash |
| CrashTransition | snowChunks | int | 0-60 | 18 | Snow chunks thrown out |
| CrashTransition | shake | float | 0-1 | 0.25 | Camera shake strength |
| CrashTransition | blackAfter | float | 0.2-3 | 0.7 | Seconds before fading to black |
| CrashTransition | fadeTime | float | 0.1-2 | 0.5 | Fade duration |
| DachshundBuilder | size | float | 0.5-30 | 1 | Dachshund size |
| DachshundBuilder | outlineWidth | float | 0-0.05 | 0.012 | Outline thickness |
| DayNightController | dayTime | float | 5-300 | 40 | Seconds of full day |
| DayNightController | nightTime | float | 5-300 | 40 | Seconds of full night |
| DayNightController | transitionTime | float | 1-60 | 10 | Seconds of sunset or sunrise |
| DayNightController | dayLightIntensity | float | 0-3 | 1.25 | Sun brightness in the day |
| DayNightController | nightLightIntensity | float | 0-1 | 0.18 | Moon brightness at night |
| DogScoutBuilder | size | float | 0.5-30 | 1 | Dog size |
| DogScoutBuilder | outlineWidth | float | 0-0.05 | 0.012 | Outline thickness |
| Gamemode | maxAirTime | float | 1-10 | 4 | Seconds in the air before it counts as a win/fall |
| GhostController | possessRange | float | 2-15 | 6 | How close a target must be to possess, in body heights |
| GhostController | ghostSize | float | 0.3-1.5 | 0.7 | Ghost size relative to the body |
| GhostController | followSmooth | float | 1-15 | 5 | Camera follow smoothing |
| GhostController | animalSpeed | float | 0.2-1.5 | 0.7 | Speed of a possessed animal relative to the rider |
| GhostController | pullOutTime | float | 0.1-1.5 | 0.5 | Seconds the ghost takes to leave the body |
| GhostController | flyTime | float | 0.1-1 | 0.35 | Seconds the ghost flies to the new body |
| GhostController | slowMotion | float | 0.05-1 | 0.2 | Time scale during possession |
| GhostFluid | wobble | float | 0-0.1 | 0.025 | Jelly wobble amount |
| GhostFluid | hemFlow | float | 0-0.2 | 0.08 | Wave along the bottom edge |
| GhostFluid | drip | float | 0-0.2 | 0.07 | How far the tips drip down |
| GhostFluid | trail | float | 0-0.6 | 0.25 | How far the lower body trails when flying |
| GhostFluid | speed | float | 0.5-5 | 2.2 | Animation speed |
| OpeningScene | platformBack | float | 2-20 | 7 | Platform distance uphill of the rider, in body heights |
| OpeningScene | corpseSize | float | 0.5-3 | 1.25 | Corpse size |
| OpeningScene | corpseDistance | float | 1-6 | 2.4 | Corpse distance from the ghost |
| OpeningScene | lookSpeed | float | 20-200 | 75 | Look-around speed, degrees per second |
| OpeningScene | aimTolerance | float | 1-20 | 6 | How close the aim must be to the corpse, in degrees |
| OpeningScene | fadeInTime | float | 0-5 | 1.5 | Fade from black at the start |
| OpeningScene | flyTime | float | 0.2-3 | 0.8 | Seconds the ghost flies into the body |
| OpeningScene | musicVolume | float | 0-1 | 0.6 | Opening music volume |
| OpeningScene | musicFade | float | 0-5 | 1.5 | Music fade time |
| PossessionMusic | volume | float | 0-1 | 0.6 | Music volume |
| PossessionMusic | fadeTime | float | 0-5 | 1.5 | Crossfade time in seconds |
| SceneOutline | outlineWidth | float | 0-0.1 | 0.02 | Outline width relative to rider height |
| SceneOutline | checkEvery | float | 0.5-10 | 2 | Seconds between scans for new objects |
| SkiGroove | strength | float | 0-3 | 1 | Overall strength |
| SkiGroove | bounce | float | 0-0.2 | 0.06 | Up-down bounce |
| SkiGroove | sway | float | 0-20 | 5 | Side sway in degrees |
| SkiGroove | maxLean | float | 0-45 | 20 | Max lean into a turn in degrees |
| SkiGroove | armSwing | float | 0-40 | 12 | Arm or wing swing in degrees |
| SkiGroove | tailWag | float | 0-60 | 20 | Tail wag in degrees |
| SkiGroove | earFlop | float | 0-40 | 12 | Ear flop in degrees |
| SkierController | maxSpeed | float | 20-200 | 60 | Top speed before speedBoost |
| SkierController | pushForce | float | 5-40 | 20 | Extra push when the board points downhill |
| SkierController | brakeForce | float | 5-50 | 25 | Brake force when the board is sideways (S) |
| SkierController | minSpeed | float | 0-30 | 4 | Lowest speed while pointing downhill |
| SkierController | extraGravity | float | 0-10 | 2 | Extra gravity multiplier |
| SkierController | speedBoost | float | 1-3 | 1.6 | Multiplies max speed, push and min speed |
| SkierController | maxSteerAngle | float | 20-90 | 40 | How far A/D turn the nose, in degrees |
| SkierController | edgeGrip | float | 1-10 | 6 | How hard the edges bite in a carve |
| SkierController | carveSpeedRatio | float | 0.5-1 | 0.87 | Share of straight speed kept while carving |
| SkierController | leanAngle | float | 0-30 | 10 | Visual lean into turns, in degrees |
| SkiingDachshundSpawner | raiseUp | float | 0-0.5 | 0.2 | Lifts the dachshund model above the snow |
| SkiingDogSpawner | animalCap | int | 10-400 | 150 | Max animals on the mountain |
| SkiingDogSpawner | nearCount | int | 0-20 | 3 | Animals kept just ahead of the player |
| SkiingDogSpawner | topSpawnEvery | float | 0.1-5 | 0.4 | Seconds between spawns at the summit |
| SkiingDogSpawner | fromBehindAtOnce | int | 0-10 | 2 | Animals passing the player from behind at once |
| SkiingDogSpawner | fromBehindEvery | float | 0.5-10 | 1 | Seconds between passers from behind |
| SkiingDogSpawner | frontEvery | float | 0.5-10 | 1 | Seconds between spawns ahead of the player |
| SkiingDogSpawner | slowestSpeed | float | 1-10 | 2.5 | Slowest cruise speed |
| SkiingDogSpawner | fastestSpeed | float | 2-15 | 5.5 | Fastest cruise speed |
| SkiingDogSpawner | passSpeedUp | float | 1-2 | 1.35 | Speed multiplier while passing the player |
| SkiingDogSpawner | dogSize | float | 0.5-30 | 1 | Animal size when Match Player Size is off |
| SlopeDecorator | countPerSlope | int | 0-60 | 15 | Trees and bears per slope piece |
| SlopeDecorator | sizeVariation | float | 0-0.5 | 0.15 | Random size difference |
| SlopeDecorator | spacing | float | 0.5-5 | 1.5 | Minimum gap between props |
| SlopeDecorator | maxSlopeAngle | float | 10-80 | 50 | Steepest ground a prop can stand on |
| SlopeExtender | extraPieces | int | 1-10 | 2 | How many slope pieces to add |
| SlopeWalls | wallHeight | float | 1-20 | 6 | Wall height |
| SlopeWalls | inset | float | -5-5 | 0 | Moves the walls inward from the snow edge |
| SlopeWalls | segmentLength | float | 2-20 | 6 | Length of each wall piece |
| SnowWeatherManager | clearDuration | float | 10-300 | 90 | Seconds of clear weather |
| SnowWeatherManager | snowDuration | float | 5-120 | 20 | Seconds of snowstorm |
| SnowWeatherManager | maxSnowflakes | int | 500-20000 | 5000 | Snowflakes on screen |
| SnowWeatherManager | snowFogStart | float | 10-500 | 60 | Fog start distance while snowing |
| SnowWeatherManager | snowFogEnd | float | 100-2000 | 350 | Fog end distance while snowing |
| SnowWeatherManager | skierWindStrength | float | 0-1 | 0.15 | How hard the wind pushes the skier |
| SnowWeatherManager | snowWindStrength | float | 0-3 | 1 | How hard the wind blows the snowflakes |

