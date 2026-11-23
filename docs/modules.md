# Les modules

Un seul plugin, « Endurance Manager » dans SimHub, fait de modules que chaque pilote active ou coupe (comme les modules
du site). Deux groupes, noms choisis par Nathan le 19/11/2026 :

## Pour moi (SimHub seul, sans le site)

| Module | Ce qu'il fait | Fichiers | Réglage (`Settings`) |
| --- | --- | --- | --- |
| **Overlay relais** | Après chaque tour : temps, énergie (ou carburant) et usure des pneus face à la cible. Overlays Dash Studio « Bandeau » et « Colonne ». | `Practice.cs`, `Dashes.cs` | `Overlays` (activé) |
| **Réglages voiture en course** | Profils perso de black box (Sec, Inter, Pluie) par voiture, appliqués en une touche. | `Profiles.cs`, `ProfilesControl.cs` | `Profiles` (coupé) |
| **Black box sur boutons** | FMT companion 0.5 tel quel, livré dans la DLL du plugin : black box et menu des stands sur boutons (volant, Stream Deck). Dash « Black box » : la page « Arrêt aux stands » du jeu sur l'écran du volant (VoCore). | `Companion.cs`, `BlackBoxDash.cs` | `Companion` (coupé pour un nouveau pilote, activé si sa DLL était déjà là) |
| **Mur des stands** | Le mur de sa propre voiture dans SimHub (dash « Endurance Manager · Mur », 2560 × 1440), sans le site. | `Wall.cs`, `WallDash.cs`, `WallProperties.cs`, `LocalRoom.cs` | `Wall` (coupé) |

Les « chiffres de course » (propriétés façon NeoRed pour les dashs, `Course.cs`, `Telemetry.cs`, `Computed.cs`) ne
sont plus un module : ils tournent quand Overlay relais ou Mur des stands est activé.

## Avec mon équipe (via le site, code de liaison)

| Module | Ce qu'il fait | Fichiers | Réglage |
| --- | --- | --- | --- |
| **Télémétrie Mémo** | Envoie les tours au site (fiches combo : une fiche par voiture + circuit, énergie, pneus, rythme, simulateur d'arrêt). | `Sync.cs`, `Live.cs`, `LmuApi.cs` | code de liaison |
| **Mur des stands équipages** | Envoie sa voiture et le classement au site une fois par seconde : les coéquipiers suivent sur la page « Mur des stands ». | `LiveShare.cs` | `Share` (activé) |
| **Aide au développement** | Journal léger (erreurs, appuis, états) et bouton « Signaler un problème », envoyés au site. Optionnel. | `Report.cs` | `Report` (coupé) |

## Commun

- Lecture du jeu : `Live.cs` (mémoire partagée `LMU_Data`), `Telemetry.cs`, `LmuApi.cs` et `GameApi.cs` (API locale
  `http://localhost:6397`, lecture seule), `Standings.cs` (WebSocket `ws://localhost:6398`). Fiables, validés en piste.
- Écran : `SettingsControl.cs` (onglets Général / Pour moi / Avec mon équipe), `Guide.cs` (parcours guidé au premier
  lancement), `Theme.cs` (couleurs du site).
- `Update.cs` (mise à jour), `Tape.cs` (enregistreur de séance), `Lovely.cs` (fiches circuit/voiture Lovely Sim Racing).

## État au 22/11/2026 (version 0.1.0)

- Numérotation repartie de 0.1.0 (ancienne 2.6.0 = point de départ). Zip 2.6.0 de test donné à quelques pilotes ; le
  site sert encore la 2.2.0 aux anciens plugins.
- Pas encore vérifié dans le vrai SimHub : les profils (appels Control Mapper signalés par `ApiCheck`), Black box sur
  boutons intégré, le mur dans Dash Studio (liaisons de position, `blink`, polices).
- Ancienne branche `mur-dash-maquette` (supprimée le 23/11/2026, dernier commit `48531fc`) : une refonte du dash du
  mur jamais fusionnée, abandonnée au profit du mur en fenêtre de la 0.1.0 (voir [mur-des-stands.md](mur-des-stands.md)).
- En discussion : remplacer l'écran en onglets par une page d'accueil avec tous les modules, et servir le mur et
  l'overlay en page web locale (voir [limites-simhub.md](limites-simhub.md)).
