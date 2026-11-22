# L'écran du plugin (page web, 22/11/2026)

Maquette validée par Nathan : https://claude.ai/artifact/4Mk7xop6UwQkNpvoPejYte (charte du site : Arial gras, titres
crème, petits titres or, fond à motif circuit, 4 bandes sous le bandeau, boutons cyan en dégradé).

- **La page** : `src/ecran/index.html` (+ `bandeau.jpg`, `tech-pattern.svg`), rangée dans la DLL (EmbeddedResource
  `ecran/…`). Menu à gauche : Accueil ; Pour moi (Overlay relais, Réglages voiture en course, Black box sur boutons, Mur
  des stands) ; Avec mon équipe (Télémétrie Mémo, Mur des stands équipages, Aide au développement) ; Assistance (Guide,
  Signaler un souci, Mises à jour). Sous 900 px de large, le menu passe en icônes.
- **Accueil** : état de SimHub, LMU et du site (barre de couleur à gauche), le partage en 3 interrupteurs avec leur « ? »
  (Mur des stands, Mur des stands équipages, Télémétrie Mémo), la voiture en cours, les 7 modules.
- **Guide** : 4 étapes (Bienvenue, Modules, Site facultatif, C'est prêt), ouvert tout seul tant que `GuideDone` est faux.
- **Données** : `ScreenApi.cs`. `GET /etat` (tout ce que la page montre, JSON) et `POST /action` (`{ do: "…" }`).
  Servies par `WallServer.cs`, **seulement** pour ce PC : connexion locale, en-tête `X-EM: 1` (aucun autre site ne peut
  l'envoyer sans demander) et `Host` localhost. La page sous `/ecran/` n'est servie qu'en local.
- **Le serveur** tourne toujours sur ce PC (pour l'écran) ; il s'ouvre au wifi seulement quand le module Mur des stands
  est allumé (`Start(network)`), sur le même port si possible.
- **Dans SimHub** : `WebScreen.cs` prend le contrôle WebView2 de SimHub par réflexion (`Microsoft.Web.WebView2.Wpf`,
  dossier de données `%LOCALAPPDATA%\EnduranceManager\ecran`). Absent ou en échec : `Shell` remet l'écran WPF
  (`SettingsControl`, `Guide`). La page Guide a aussi un bouton « Ouvrir l'ancien écran ».
- **Black box sur boutons** : pour l'instant, « Régler mes boutons » ouvre l'écran de FMT companion dans une fenêtre.
  À refaire dans la page (onglets Préparation / Commandes, voir la maquette) quand FMT companion sera réécrit.
- **Photos** : `tests/Preview` les fait avec Edge sans fenêtre (`#profils`, `#guide:1`… ouvrent une page).
- **Non vérifié sur un vrai PC** : présence de WebView2 dans le dossier de SimHub, rendu dans l'onglet du plugin.
