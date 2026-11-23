# Réglages voiture en course et Black box sur boutons

## Black box sur boutons (FMT companion)

- FMT companion = plugin SimHub de Nathan (`FMTCompanionPlugin`, version 0.5). **Son code source est perdu**, seule la
  DLL existe : `src/fmt/FMTCompanion.dll` (copie de `FMT Companion 0.5.zip`, SHA-256 `aac2eba5…2e4f`).
- Elle est **dans la DLL du plugin** (ressource `fmt/FMTCompanion.dll`) : la mise à jour du plugin la donne à chaque
  pilote, rien à installer à part. Au lancement, module activé, `Companion.Unpack` l'écrit dans
  `SimHub\EnduranceManager\` (ou `%LOCALAPPDATA%\EnduranceManager\` si le dossier de SimHub demande les droits admin).
- `Companion.cs` la charge telle quelle (`Assembly.LoadFrom` depuis ce fichier), lui
  passe `Init` / `DataUpdate` / `End`, et affiche son écran WPF dans l'onglet du module. L'installeur déplace un
  `FMTCompanion.dll` installé seul dans ce dossier (jamais deux fois dans SimHub).
- Comment FMT companion agit : le menu des stands est **écrit par l'API LMU** (`GET rest/garage/PitMenu/receivePitMenu`,
  puis `POST` du menu complet à `rest/garage/PitMenu/loadPitMenu`, puis relecture). Les réglages voiture (TC, ABS, map
  moteur, répartition, migration, barres, régén…) = commandes LMU reliées (« Préparer LMU », colonne secondaire de LMU)
  à des boutons de sortie vJoy ou d'une clé bridge SimHub, déclenchés par des rôles du Control Mapper
  « FMT - <famille> - <commande> ».
- Nathan n'utilise plus vJoy mais une clé USB bridge SimHub ; beaucoup de pilotes en ont une. Le plugin doit marcher
  avec les deux : passer par les rôles du Control Mapper le permet.


### Black box sur l'écran du volant (VoCore)

- Demande d'un pilote (23/11/2026) : voir la black box de LMU sur l'écran VoCore de son volant et la masquer sur
  l'écran principal. LMU ne sait pas afficher sa black box ailleurs, et une copie d'image est impossible une fois
  masquée : on la **redessine** avec les données du jeu, d'après les captures de Nathan (maquette :
  [`maquettes/black-box-vocore/`](../maquettes/black-box-vocore/index.html)).
- Étape 1 (faite) : la page « Arrêt aux stands ». Dash « Endurance Manager · Black box » (800 × 480, écrit dans les
  modèles de SimHub comme les overlays, `BlackBoxDash.cs`) : titre, 7 lignes visibles, ligne choisie en blanc et rouge
  avec ses flèches, flèches de défilement. Les lignes sont celles du menu lu par l'API (`PitMenu`, lu toutes les 0,5 s
  quand le module est activé) ; dommages, pénalités, pilote suivant arrivent tout seuls quand le jeu les ajoute.
- **Le jeu ne donne ni la position de son curseur ni la page affichée.** La ligne en rouge est la dernière dont le
  réglage a changé (avec les boutons directs de FMT companion, celle que le pilote vient de régler) ; la fenêtre de
  7 lignes défile pour la garder visible.
- Noms : si l'API répond en anglais (« VIRTUAL ENERGY »), ils sont écrits comme l'écran français du jeu
  (« Énergie Virtuelle: ») ; sinon tels que le jeu les donne. À vérifier dans le vrai jeu : la langue de l'API et le
  texte de l'énergie (le jeu affiche « 100% 35 laps », l'API peut ne donner que « 100% »).
- Propriétés : `BlackBox.Lignes`, `.Choisie`, `.Haut`, `.Bas`, `BlackBox.1.Nom` / `.Valeur` / `.Choisie` … `BlackBox.7.*`.
- Étape 2 (à faire, accord de Nathan) : les autres pages (Relatif, Séance, Chrono, Pneus, Carburant, Électronique) et
  un bouton SimHub « page suivante » pour le VoCore.

## Réglages voiture en course (profils)

- Chaque pilote garde, voiture par voiture, ses réglages de black box : 3 profils par défaut **Sec / Inter / Pluie**.
  Profils perso, sur son PC (`%LOCALAPPDATA%\EnduranceManager\profils.json`).
- Réglages d'un profil : TC, TC cut, TC slip, ABS, répartition de freinage, migration, map moteur, régén (un réglage
  absent de la voiture est ignoré). Nathan voulait aussi le menu des stands (18 lignes) : pas encore fait.
- Un bouton par profil (actions SimHub `Profil1`…) : 1er appui = un popup montre ce qui va changer (en roulant et au
  prochain arrêt) ; 2e appui sur le même bouton dans les 5 s = appliquer ; sans 2e appui, rien ne change. Appui long 2 s =
  mémoriser l'état actuel. Règle décidée par Nathan le 22/11/2026.
- Le popup (`src/ProfilePopup.cs`) : petite fenêtre toujours au premier plan, que le clic traverse et qui ne prend pas le
  focus du jeu (visible au-dessus de LMU en fenêtré sans bordure). « Placer le popup » sur l'onglet la fait glisser, la
  place est gardée. Mêmes textes pour les dashs : `Profils.Popup`, `.Titre`, `.Lignes`, `.Aide`, `.Confirmer`.
- Appliquer = appuyer sur les rôles « + / − » trouvés tout seuls (`Profiles.Find` : rôles FMT companion d'abord, puis
  Next/Previous ou +/−), relire la valeur après chaque appui, s'arrêter et signaler si elle ne bouge pas.
- **Rien d'automatique.** Seulement des rappels : profil pas actif (valeurs différentes) et humidité de la piste
  (plages par défaut : Sec < 5 %, Inter 5–12 %, Pluie > 12 %).
- Écran simplifié le 19/11/2026 : la voiture + 3 cartes (valeurs, Mémoriser, Appliquer) + une ligne sur les boutons.

API SimHub utilisée (vérifiée le 19/11/2026 sur le PC de Nathan) : `pluginManager.GetControlMapperInterface()` →
`StartRole(nom)`, `StopRole(nom)`, `StartStopRole(nom, ms)`, `GetAvailableButtonRoles()`. Aucune API pour créer un
rôle : c'est un nom tapé sur un bouton virtuel de sortie dans le Control Mapper. Les types de retour dans la façade
(`reference/`) sont devinés : `ApiCheck` les signale tant qu'ils n'ont pas été confirmés sur une DLL compilée avec le
vrai SimHub (`dotnet run --project reference/ApiCheck -- --ecrire <dll>`).

À vérifier sur le PC de Nathan : les noms des rôles FMT trouvés correspondent vraiment ; le niveau de régén n'est pas
lisible dans la mémoire en roulant (seulement en kW).
