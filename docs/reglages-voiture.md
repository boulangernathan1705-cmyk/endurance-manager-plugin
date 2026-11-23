# Réglages voiture en course et Black box sur boutons

## Black box sur boutons (FMT companion)

- FMT companion = plugin SimHub de Nathan (`FMTCompanionPlugin`). Son code a été retrouvé le 23/11/2026 en décompilant
  la DLL 0.5 (ILSpy) : il est dans `src/fmt/` (projet `FmtCompanion.csproj`, version 0.6.0), compilé par la CI. Depuis 0.6.0 :
  plus de lecture du menu en boucle (il prend celle du plugin, `Companion.Offer`), plus de recherche de LMU toutes les 10 s.
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
- **Sa page dans l'écran du plugin** (maquette validée le 23/11/2026, `maquettes/black-box-boutons/`) : `CompanionPage.cs`
  refait son écran dans la page web (état LMU / Control Mapper / sortie, 3 étapes, commandes à cocher par Stand /
  Voiture / Caméra / Poste avec Tester, Préparer LMU, Annuler, Control Mapper, profil Stream Deck, sauvegarde, lecture
  seule après une mise à jour de LMU). Comme son code source est perdu, la page lui parle **par réflexion**, avec les
  mêmes appels que son propre écran (`CompanionControl`, relu dans sa DLL) : `SetCommandVisible`, `Mapper.Test`,
  `Preparer.Prepare` (commandes LMU cochées), `Preparer.Undo`, `Mapper.OpenMapper`, `ExportStreamDeckProfile`,
  `BackupMapping`, `AcknowledgeLmuVersion`, `Service.EnableWrites`, `SelfCheck`, `Preparer.State`. Les lignes ne sont
  demandées que quand la page est ouverte (`/etat?bb=1`). Son ancien écran reste dans « Plus » en secours.


### Black box sur l'écran du volant (VoCore)

- Demande d'un pilote (23/11/2026) : voir la black box de LMU sur l'écran VoCore de son volant et la masquer sur
  l'écran principal. LMU ne sait pas afficher sa black box ailleurs, et une copie d'image est impossible une fois
  masquée : on la **redessine** avec les données du jeu, d'après les captures de Nathan (maquette :
  [`maquettes/black-box-vocore/`](../maquettes/black-box-vocore/index.html)).
- Étape 1 (faite) : la page « Arrêt aux stands ». Dash « Endurance Manager · Black box » (800 × 480, écrit dans les
  modèles de SimHub comme les overlays, `BlackBoxDash.cs`) : titre, 7 lignes visibles, ligne choisie en blanc et rouge
  avec ses flèches, flèches de défilement. Les lignes sont celles du menu lu par l'API (`PitMenu`, lu toutes les 0,5 s
  quand le module est activé) ; dommages, pénalités, pilote suivant arrivent tout seuls quand le jeu les ajoute.
- **Le jeu ne donne ni la position de son curseur ni la page affichée.** Le dash a donc son propre curseur (0.1.5,
  demande de Nathan : régler sans ouvrir la black box du jeu). Six actions SimHub, les six commandes MFD de LMU, à relier
  à des boutons du volant dans « Contrôles et événements » : `BlackBoxMonter` / `BlackBoxDescendre` (ligne, rien
  n'est envoyé au jeu), `BlackBoxPageSuivante` / `BlackBoxPagePrecedente` (sans effet tant qu'il n'y a qu'une page),
  `BlackBoxPlus` / `BlackBoxMoins` (valeur : menu lu, ligne au choix suivant ou précédent, menu entier renvoyé à
  `loadPitMenu` puis relu, comme les profils). Comme dans la black box du jeu (essai de Nathan) : les choix font le
  tour dans les deux sens, « Pneus » règle les 4 roues, la valeur s'affiche dès l'appui (envoi à part, le dernier
  menu voulu seulement). Une ligne changée ailleurs
  (bouton FMT companion, black box du jeu) amène le curseur sur elle. **Retirer + et − des commandes MFD de LMU** sur
  les mêmes boutons, sinon un appui change la valeur deux fois.
- Les mêmes six en **rôles du Control Mapper** (`BlackBoxRoles.cs`, demande de Nathan : « Contrôles et événements »
  marche mal) : « EM - Black box - Monter / Descendre / Page suivante / Page precedente / Plus / Moins », créés au
  lancement quand Black box sur boutons est activé, comme ceux de FMT companion (bouton virtuel `VirtualButtonMap` de
  la sortie du Control Mapper, `SaveSettings`, lu dans sa DLL). Un appui arrive par `InputPressed`
  (« ControlMapperPlugin.<rôle> »). Tout par réflexion ; jamais « FMT » au début (FMT companion retire les rôles FMT
  qu'il ne connaît pas). Leur état : propriété `BlackBox.Roles`. À vérifier dans le vrai SimHub.
- Toucher : boutons transparents de Dash Studio (`ButtonItem`, `TriggerAction` = « EnduranceManagerPlugin.<action> »,
  format lu dans Lovely Dashboard sur le PC de Nathan). Dash Studio ne connaît que le toucher, pas le glissé, et des
  boutons superposés partent tous ensemble : aucune zone ne se chevauche. Une autre ligne touchée est choisie (actions
  `BlackBoxLigne1` … `7`) ; la 1re ou la dernière ligne affichée fait aussi défiler d'une ligne. La ligne choisie (rouge)
  est coupée en deux : moitié gauche = −, moitié droite = +. Les bandes de 60 px tout à gauche / tout à droite changent
  de page. Les choix « INVALID » du jeu sont sautés. À vérifier sur iPhone et VoCore.
- Noms : si l'API répond en anglais (« VIRTUAL ENERGY »), ils sont écrits comme l'écran français du jeu
  (« Énergie Virtuelle: ») ; sinon tels que le jeu les donne. À vérifier dans le vrai jeu : la langue de l'API et le
  texte de l'énergie (le jeu affiche « 100% 35 laps », l'API peut ne donner que « 100% »).
- Propriétés : `BlackBox.Page`, `.PageNo` (0 Relatif … 6 Arrêt aux stands), `.Message`, `.Appuis` (derniers appuis reçus), `.Attente`, `.Haut`, `.Bas`,
  `.Lignes`, `.Choisie` ; les 7 lignes `BlackBox.1.Nom` / `.Valeur` / `.C1` `.C2` `.C3` / `.Pos` / `.Classe` / `.Couleur` /
  `.Petit` / `.Choisie` / `.Reglable` / `.Moi` … `BlackBox.7.*` ; les roues `BlackBox.Pneus.AVG.Usure` / `.Gomme` /
  `.GommeCouleur` / `.Reste` (0 à 100, l'anneau) / `.Pression` / `.Temp` / `.Frein` / `.Etat` / `.Couleur` … `ARD.*`, `BlackBox.Pneus.Jeux`.
- Étape 2 (faite, PR #14, accord de Nathan sur les pages le 23/11) : les 7 pages du jeu dans son ordre (Relatif, Séance,
  Chrono, Pneus, Carburant, Électronique, Arrêt aux stands), icônes dans la barre de titre, ◂ ▸ font le tour
  (`BlackBoxPages.cs`). Boutons par page, comme la black box du jeu (Nathan) :
  - Électronique : ▲ ▼ changent de ligne, − + envoient les commandes de FMT companion (`TC.Next`, `Bias.Forward`…,
    comme son bouton « Tester » ; ses ids sont `Key.TC.Next`…, trouvés avec ou sans `Key.`) : il faut avoir fait
    « Préparer LMU » dans FMT companion. Pas de passage par l'API du jeu : aucune adresse connue pour ces réglages. Seuls les réglages que la
    voiture a sont listés.
  - Chrono : − + passent de Delta à Secteur à Split ; ▲ ▼ de Timing à Relatif. Comme les captures de Nathan :
    Secteur = temps à la fin de chaque secteur, Split = temps de chaque secteur, toujours « 0:33.396 ». Timing :
    Meilleur, Optimal (S1 aussi pris du meilleur S1 du jeu), puis le plus rapide de la catégorie s'il bat mon meilleur
    tour. Relatif : Dernier, puis la voiture juste devant et juste derrière au classement (leur dernier tour).
  - Carburant : − + passent de Virtual Energy à Carburant ; ▲ un tour de plus pour l'objectif de sauvegarde, ▼ un de
    moins. Pousser = la conso par tour prévue par le jeu (écran des stands, lu au stand), sinon la plus forte des conso
    (dernier tour, moyenne de 5) ; Estimation = moyenne de 5, sinon la prévision du jeu ; avec un
    objectif, la conso par tour qu'il faut et ⚠ si l'estimation n'y arrive pas.
  - Relatif, Séance, Pneus : rien à régler. Relatif : les voitures au garage sont cachées, l'écart en temps se base
    sur le meilleur tour de la séance. Catégories : Hypercar rouge, LMP2 bleu (ELMS bleu clair), LMP3 violet, LMGT3
    vert, GTE orange. Pneus : l'anneau s'use (36 points), lettre de la gomme dans sa couleur (capture de Nathan : soft
    blanc, medium jaune, hard rouge, pluie bleu clair), température = moyenne de la bande de roulement (à comparer),
    jeux comptés à partir de la qualif. « Mixte » n'est jamais proposé par − + sur « Pneus ».
  Au toucher, les lignes réglables (en-têtes de Chrono et Carburant, ligne choisie) sont coupées en deux (− / +, actions
  `BlackBoxToucheMoins1…7` / `BlackBoxTouchePlus1…7`).
  À vérifier dans le jeu (jamais vus, faits à notre idée) : écart du Relatif (devant « -1.2 », derrière « +1.2 ») et
  couleurs des catégories sauf Hypercar ; textes d'adhérence ; contenu exact des lignes Delta ; « P4 » des ARB
  (niveau + 1) ; migration, carto moteur et recharge affichées en niveau (le jeu écrit « 1.5% F », « 120kW ») ; roue
  crevée ou perdue sur la page Pneus.

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
