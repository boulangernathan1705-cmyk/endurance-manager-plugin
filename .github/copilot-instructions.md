# Instructions GitHub Copilot — Plugin Endurance Manager

Lire `AGENTS.md`, `docs/README.md`, `docs/ai-workflow.md` et les documents du module. Les décisions validées dans `docs/decisions.md` et les règles communes s'appliquent également aux revues.

- Répondre en français simple ; Nathan ne code pas. Proposer une maquette et attendre son accord pour une nouvelle fonction ou un nouvel écran ; prendre en charge les petites corrections et les tests autorisés.
- Travailler exclusivement sur GitHub : partir de `main`, une branche et PR par chantier, un seul chantier fonctionnel à la fois. Pas de fusion ni de publication sans validation. Ne pas augmenter la version pour de la documentation.
- LMU uniquement. Préserver les comportements télémétrie et black box validés, le fonctionnement sans site et les contrats du site/relais. Signaler explicitement tout changement de ces comportements.
- Compiler avec la façade SimHub si nécessaire et utiliser la CI Windows pour ApiCheck, rejeu et aperçus ; lire `docs/tests-et-ci.md`. Ne pas confondre un rejeu avec un essai réel. Ne pas réclamer de secret pour accéder au site.
- En revue, privilégier les régressions concrètes, les contrats, la sécurité, les performances et les contrôles manquants. Mettre à jour les documents concernés et décrire ce qui reste à tester. Aucun dialogue automatique entre IA.
