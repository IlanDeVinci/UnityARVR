# Restaurant Pokemon VR

Simulation VR d'un restaurant japonais thème Pokémon, développée sous Unity avec le **XR Interaction Toolkit 3.4.1** et **URP**. Compatible Meta Quest (Android) et PC-VR (OpenXR).

---

## Sommaire

- [Démarrage](#démarrage)
- [Contrôles VR](#contrôles-vr)
- [Fonctionnalités](#fonctionnalités)
- [Pipeline de cuisine](#pipeline-de-cuisine-pikachu)
- [Architecture](#architecture)
- [Scripts principaux](#scripts-principaux)
- [Assets externes](#assets-externes)

---

## Démarrage

### Prérequis
- **Unity 6** (ou compatible XR Interaction Toolkit 3.4.1)
- **Android Build Support** pour Meta Quest (`AndroidMinSdkVersion: 32` / Android 12L)
- **OpenXR Plugin** activé

### Lancer
1. Ouvrir `Assets/Scenes/MainScene.unity`
2. Mettre le casque Quest (Link ou build APK)
3. Appuyer sur **JOUER** sur le menu d'accueil

---

## Contrôles VR

| Action | Contrôleur |
|--------|-----------|
| **Déplacement** (smooth) | Stick gauche |
| **Rotation caméra** (smooth turn) | Stick droit |
| **Pointer/Interagir** | Ray rouge depuis les deux mains |
| **Grab à distance** (far-grab blaster) | Pointer + `Grip` → l'objet vole vers la main |
| **Grab au contact** (near-grab) | Toucher + `Grip` |
| **Lancer** | Relâcher `Grip` pendant un mouvement (throw physique) |
| **Tourner un objet tenu** | Stick de la main qui tient (X = yaw, Y = pitch) |
| **Menu objets** | Bouton menu (`Y` par défaut) |
| **Spawn depuis menu** | Cliquer sur un objet → cliquer **PLACER** |

Le joueur est **15% plus grand** que la normale (scale XR Origin à 1.15) pour une meilleure visibilité des objets.

---

## Fonctionnalités

### Menu principal
- Canvas World Space affiché au lancement devant le joueur
- Bouton **JOUER** qui lance le jeu et détruit le menu
- Musique "Menu Pixelisé.mp3" en boucle via `AudioManager`

### Menu de sélection d'objets
- **Grid 4 colonnes scrollable** avec les **20 assets** placeables
- **Preview 3D** générée à chaud pour chaque modèle (caméra isolée → RenderTexture → Texture2D)
- **Flow select-then-place** : clic sur un item → surbrillance dorée → clic **PLACER** → spawn
- **Panneau capteurs** en haut à droite : données live de l'API **Chain (MIT Media Lab)** — Tidmarsh water temperature
- Les objets spawnés sont automatiquement **grabbables** avec far-grab blaster

### Système de grab (XR)
- **Far-grab blaster** : pointer à distance + grip → l'objet vole vers la main (`InteractableFarAttachMode.Near`)
- **VelocityTracking** : mouvement physique fluide, throw au relâchement avec élan conservé
- **Rotation en main** : stick de la main qui tient l'objet le fait pivoter (yaw/pitch relatifs à la caméra)
- **Auto-setup** : tous les objets spawnés reçoivent `Rigidbody + BoxCollider + XRGrabInteractable`

### Portes
- **Porte Japonaise** coulissante (slide forward puis right pour s'ouvrir)
- Détection via `XRSimpleInteractable` — clic pour ouvrir/fermer
- Sons open/close (qubodup Door Set, CC0)
- Bounding box auto-calculée depuis les MeshRenderers enfants (gère les GLB hiérarchiques)
- `DoorAutoSetup` peut configurer automatiquement des portes depuis le nom ou tag

### Interrupteur
- **Animation du rocker** directe en code (pas besoin d'Animator Controller)
- Rotation extraite du GLB (`TurnOn` / `TurnOff` → ±0.0749 quaternion X)
- Contrôle une **liste de lumières** assignables dans l'inspector
- Son de clic (switch1.wav, CC0 Kenney)

### Pikachu AI
- **`PikachuSpawner`** instancie 20 Pikachus au Start
- **`PikachuWander`** : wander aléatoire + fuite quand le joueur s'approche
- **Détection de murs** via raycasts en éventail + séparation entre Pikachus
- **Grabbable** : stick de la main tournant = rotation, throw au release
- **Sons** : cri de fuite (hurt), cri attrapé (cute), cri lancé (grunt) — pack rubberduck CC0

### Poubelle
- Détecte tout `Rigidbody` entrant dans la zone trigger
- Remonte au GameObject root et le détruit
- **Protection** : XR Origin, camera, controllers, GameManager, EventSystem **jamais détruits**
- Son de chute au moment de la destruction

### Skybox
- Cubemap sunset (Sand Castle Studio, CC0 OpenGameArt)
- `Shader Graph/Skybox/Panoramic` avec layout 6 frames
- Ambient lighting ajusté pour tons chauds (coucher de soleil)

### Données capteur live
- **API Chain (MIT Media Lab)** : `https://chain-api.media.mit.edu/scalar_sensors/12085`
- Endpoint : `water_temperature` (site Tidmarsh, Massachusetts)
- Parsing HAL+JSON via `JsonUtility` (avec remplacement de `"ch:device"` → `"chdevice"` pour contourner la limitation de Unity)
- Affichage riche : icône emoji, valeur colorée, âge relatif ("il y a X min/h/j/an"), statut actif (LED verte/rouge)
- Refresh automatique toutes les 15s
- Traductions FR (water_temperature → "Température de l'eau", humidity → "Humidité", etc.)

---

## Pipeline de cuisine Pikachu

Un système de cuisine en 4 étapes :

```
Pikachu qui court  ─┐
                    ├──► [ Planche à découper ]  ──►  Pikachu couché
                    │         (OnTriggerEnter)
                    │
Couteau Scalpereur ─┤
                    ├──► Touche le Pikachu couché  ──►  Pikachu découpé  (grabbable)
                    │
                    ├──► Dans la [ Poêle vide ]   ──►  Pikachu_poele_premium
                    │         (FryingPan)
                    │
                    └──► Sur [ Plaque de cuisson ] (node 17 du plan_de_travail)
                              ├─ Fumée 💨 (ParticleSystem)
                              ├─ Minuteur 3D (30s, jaune→rouge)
                              ├─ "C'EST CUIT !" (2.5s)
                              └─ Remplacé par → poele_steak (grabbable)
```

**Scripts impliqués :**
- `CuttingBoard.cs` (planche) — étapes 1 et 2
- `KnifeSetup.cs` (couteau Scalpereur_Lame_Parfaite) — far-grab blaster
- `FryingPan.cs` (poêle vide) — étape 3
- `CookingPlate.cs` (plan_de_travail_cuisine) — étape 4, animation + timer + transformation

**Détection intelligente** : la plaque détecte uniquement les poêles **avec un pikachu dedans** (filtre sur nom contenant "poele" + "pikachu" + pas "vide").

---

## Architecture

### Scène `MainScene.unity`
- **XR Origin (XR Rig)** — Starter Assets prefab avec NearFarInteractor sur chaque main
- **GameManager** (GameObject racine) — porte : `GameManager`, `AudioManager`, `SceneResetManager`, `ObjectSpawner`, `SpawnMenuUI`, `SceneAssetCleaner`, `GrabRotator`
- **Restaurant_Japonais (1)** — modèle GLB avec `DoorAutoSetup`
- **plan_de_travail_cuisine** — avec `CookingPlate`
- **poele_premium_vide** — avec `FryingPan`
- **planche_a_decouper** — avec `CuttingBoard`
- **Scalpereur_Lame_Parfaite** — avec `GrabbablePlaceable` + `XRGrabInteractable` (FarAttach=Near)
- **Porte_Japonaise** — avec `SlidingDoorController`
- **interrupteur_eteint** — avec `LightSwitchSetup`
- **Poubelle_Miasmax** — avec `TrashBin`
- **PikachuSpawner** — GameObject dédié
- **MainMenu** — Canvas World Space avec `MainMenuUI`

### Dossiers
```
Assets/
├── Audio/
│   ├── Music/Menu Pixelisé.mp3
│   └── SFX/              (165 fichiers : UI, Door, Switch, Pikachu, Grab, Footsteps)
├── Models/               (modèles GLB/FBX originaux)
├── Prefabs/              (prefabs wrappers, pikachu_qui_court)
├── Resources/
│   ├── Furniture/        (20 modèles pour ObjectSpawner)
│   ├── pikachu_couche.glb
│   ├── pikachu_decoupe.glb
│   ├── pikachu_poele.glb
│   └── poele_steak.glb
├── Scenes/MainScene.unity
├── Scripts/
│   ├── Interaction/      (doors, cooking, grab, trash, knife)
│   ├── Managers/         (AudioManager, GameManager, SceneAssetCleaner, SceneResetManager)
│   ├── Player/           (PlayerController, CameraController, FootstepPlayer)
│   ├── UI/               (MainMenuUI, SpawnMenuUI, SensorDataFetcher, ModelPreviewGenerator)
│   └── Editor/           (RestaurantBuilder — outil éditeur)
├── Settings/             (URP assets)
└── Skybox/               (cubemaps sunset)
```

---

## Scripts principaux

| Script | Rôle |
|--------|------|
| `MainMenuUI` | Menu JOUER au lancement, World Space Canvas |
| `SpawnMenuUI` | Grid scrollable avec previews 3D, flow select-then-place |
| `ModelPreviewGenerator` | Génère des Texture2D preview via caméra isolée + RenderTexture |
| `SensorDataFetcher` | Fetch API Chain (MIT) + affichage rich text |
| `ObjectSpawner` | Spawn dynamique depuis `Resources/Furniture/` avec auto-grab |
| `GrabRotator` | Rotation des objets tenus via thumbsticks |
| `KnifeSetup` | Far-grab blaster pour le couteau |
| `SlidingDoorController` | Porte coulissante avec XR interaction |
| `LightSwitchSetup` | Interrupteur avec animation rocker + contrôle lumières |
| `PikachuWander` | IA wander + flee, gère grab/throw |
| `PikachuSpawner` | Spawn N Pikachus, assigne les sons |
| `CuttingBoard` | Étapes 1 et 2 de la cuisine (pose + découpe) |
| `FryingPan` | Étape 3 (poêle + pikachu découpé → plat) |
| `CookingPlate` | Étape 4 (cuisson 30s + FX + transformation) |
| `TrashBin` | Zone trigger qui détruit les objets entrants |
| `FootstepPlayer` | Sons de pas selon le mouvement du CharacterController |
| `SceneAssetCleaner` | Nettoie les caméras/AudioListeners parasites au démarrage |
| `AudioManager` | Musique de fond en boucle + SFX 2D |

---

## Assets externes

**Tous les assets sont libres de droits (CC0 ou équivalent).**

| Source | Utilisation |
|--------|-------------|
| **Kenney** (OpenGameArt, CC0) | 51 sons UI (clicks, rollovers, switches) |
| **qubodup** (OpenGameArt, CC0) | 18 sons de porte (DoorOpen × 8, DoorClose × 10) |
| **kddekadenz** (OpenGameArt, CC0) | 8 sons de pas (wood, stone, leaves, gravel, mud) |
| **rubberduck** (OpenGameArt, CC0) | 80 cris de créatures (cute, hurt, grunt, etc.) |
| **Summoning Wars** (OpenGameArt, CC-BY) | 6 sons d'inventaire (pickup, drop) |
| **Sand Castle Studio** (OpenGameArt, CC0) | 25 skyboxes cloudy (utilise Sunset) |
| **MIT Media Lab — Chain API** | Données capteur live (Tidmarsh) |

---

## Notes techniques

- **URP** avec `MSAA=1`, `RenderScale=0.8`, `UpscalingFilter=Automatic` pour Quest
- **SSAO désactivé** (trop coûteux sur mobile)
- **Far-grab blaster** via `InteractableFarAttachMode.Near` (objet vole vers la main)
- **GLB unnamed nodes** : la plupart des modèles GLB ont des nœuds sans nom — on les cible par index de `GetComponentsInChildren<MeshRenderer>()` ou par détection de forme (bounds door-shaped)
- **Bounds world-space** : tous les triggers/colliders sont créés avec des sizes en espace monde (pas parentés aux objets scalés) pour éviter les problèmes de scale hérité
- **URP + destroy** : `Destroy()` sur Camera/Light échoue à cause de `UniversalAdditionalCameraData` — on détruit le GameObject entier à la place

---

## Licences

Code : voir `LICENSE` à la racine.
Assets audio/skybox : CC0 / CC-BY (voir section "Assets externes").
Modèles 3D (Pikachu, Scalpereur, etc.) : fournis par l'utilisateur, droits réservés.
