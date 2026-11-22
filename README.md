# Endurance Manager · plugin SimHub

Le plugin SimHub de l'équipe FMT pour préparer et courir les endurances sur **Le Mans Ultimate** (LMU seulement).
Il marche **seul dans SimHub** ; le lien avec le site [Endurance Manager](https://endurance-manager.app) est un module
qu'on active si on veut partager avec son équipe.

## Installer (pilotes)

1. Ouvrir [Releases](../../releases/latest), télécharger `plugin-endurance-manager-x.y.z.zip`.
2. Dézipper, double-cliquer sur `Installer le plugin.cmd` : il ferme SimHub, copie le plugin et relance SimHub.

Ensuite le plugin se met à jour tout seul (bouton « Mettre à jour » dans son écran).

## Le dépôt

| Dossier | Contenu |
| --- | --- |
| `src/` | Le code du plugin (C#, .NET Framework 4.8). La lecture de LMU et les boutons de la black box sont fiables : ne pas changer leur comportement sans le dire. |
| `reference/` | Une façade de l'API SimHub (mêmes signatures, sans code) pour compiler sans SimHub, et `ApiCheck` qui liste ce qui n'a jamais été vérifié avec le vrai SimHub. |
| `tests/` | `Replay` rejoue des séances LMU dans le code du plugin, du jeu jusqu'au mur des stands. `Preview` photographie l'écran du plugin. |
| `enregistrements/` | Les enregistrements LMU (`.emrec`) faits avec le plugin : la CI les rejoue tous. |
| `docs/` | Tout ce qu'il faut savoir pour reprendre le travail sans contexte. **Commencer par [`docs/README.md`](docs/README.md).** |
| `maquettes/` | Maquettes HTML et images (écran du plugin, mur des stands, guide), et photos de l'écran faites par la CI. |

## Travailler

- `main` = ce que les pilotes installent. Chaque changement se fait sur une branche, puis une PR que Nathan valide.
- Chaque PR est compilée et testée sur Windows par GitHub (onglet Actions) : le zip à essayer est dans *Artifacts*.
- Une nouvelle version : monter `<Version>` dans `src/EnduranceManager.SimHub.csproj` dans la PR. Une fois fusionnée
  dans `main`, la CI publie la Release. Détails : [`docs/publier.md`](docs/publier.md).

Compiler chez soi (Windows, SimHub installé) : `dotnet build src/EnduranceManager.SimHub.csproj -c Release`.
Sans SimHub (cloud, Linux) : ajouter `-p:UseSimHubFacade=true`.

## Pour une IA (ChatGPT, Claude, Codex)

Lire [`AGENTS.md`](AGENTS.md) avant toute modification.
