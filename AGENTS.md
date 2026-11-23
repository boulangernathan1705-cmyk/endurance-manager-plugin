# Consignes pour une IA (ChatGPT, Claude, Codex…)

Projet de Nathan (équipe FMT). Répondre en **français**, court. Devise : « La simplicité, c'est la sophistication
suprême » : écran le plus simple, textes courts.

## Avant de coder

- Lire [`docs/README.md`](docs/README.md) puis le fichier `docs/` du sujet.
- **Nouvelle fonction ou nouvel écran : proposer d'abord** (maquette dans `maquettes/`), coder après l'accord de Nathan.
  Petite correction : directement.
- Un seul chantier à la fois sur le plugin (une branche, une PR). Regarder les PR ouvertes avant de commencer.

## Règles qui ne bougent pas

- **LMU seulement.** Jamais de règle ou d'usage iRacing (ex. le partage équitable des tours est la règle LMU Online).
- **Données brutes, aucun conseil** à l'écran.
- Le plugin marche **avec SimHub seul**. Le site est un module optionnel (« Avec mon équipe ») : aucun module « Pour
  moi » ne doit dépendre du site.
- **Ne pas changer le comportement** de la lecture LMU (`Live.cs`, `Telemetry.cs`, `LmuApi.cs`, `GameApi.cs`) ni des
  boutons de la black box (`Profiles.cs`, `Companion.cs`) sans le dire clairement dans la PR : ils sont validés en piste.
- FMT companion 0.5 est intégré tel quel (sa DLL `src/fmt/FMTCompanion.dll`, embarquée dans le plugin ; `Companion.cs`) ; son code source est perdu. Ne jamais le simplifier.
- Jamais de code copié de Strat Calculator (logiciel payant) ou d'un autre produit.
- Le format envoyé au site est un contrat : `shared/live.mjs` et `shared/combos.mjs` du dépôt
  [app-endurance-manager](https://github.com/boulangernathan1705-cmyk/app-endurance-manager). Un changement de format se
  fait des deux côtés, le site d'abord (voir [`docs/lien-avec-le-site.md`](docs/lien-avec-le-site.md)).
- Pas de barre de défilement horizontale ; textes d'explication réduits au minimum.
- Ne pas remonter `<Version>` sans raison : une version fusionnée dans `main` est publiée aux pilotes.

## Vérifier

- La CI (Windows) compile, lance `ApiCheck`, rejoue `tests/Replay` et les `enregistrements/`, photographie l'écran.
  Une PR doit être verte.
- `ApiCheck` signale tout appel SimHub jamais vérifié avec le vrai SimHub : le dire dans la PR, Nathan le teste.
- Dire comment c'est testé : rejeu, aperçu, ou SimHub réel (seul Nathan peut tester dans SimHub et LMU).
