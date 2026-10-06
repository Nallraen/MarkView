# Contribuer à MarkView / Contributing to MarkView

🇫🇷 Merci de votre intérêt ! · 🇬🇧 Thanks for your interest! (English below)

## 🇫🇷 Français

### Signaler un bug ou proposer une idée

Ouvrez une [Issue](https://github.com/Nallraen/MarkView/issues) en précisant :
- votre version de Windows et de MarkView (nom de la release téléchargée) ;
- ce que vous avez fait, ce que vous attendiez et ce qui s'est passé ;
- si possible, le fichier `.md` concerné et le journal du jour dans `%AppData%\MarkView\logs\`.

### Proposer une modification

1. Forkez le dépôt et créez une branche depuis `main`.
2. Compilez et testez (voir [docs/DEVELOPPEMENT.md](docs/DEVELOPPEMENT.md)) :
   `dotnet build MarkView.sln` doit passer **sans erreur ni avertissement**, et `dotnet test MarkView.sln` au vert.
3. Gardez le style existant : code et commentaires en anglais, textes de l'interface en français.
4. Ouvrez une pull request qui explique le **pourquoi** de la modification.

En proposant une contribution, vous acceptez qu'elle soit distribuée sous la licence du projet,
[PolyForm Noncommercial 1.0.0](LICENSE.md).

## 🇬🇧 English

### Report a bug or suggest an idea

Open an [Issue](https://github.com/Nallraen/MarkView/issues) with:
- your Windows version and the MarkView release you downloaded;
- what you did, what you expected and what happened;
- if possible, the `.md` file involved and today's log from `%AppData%\MarkView\logs\`.

### Submit a change

1. Fork the repository and create a branch from `main`.
2. Build and test (see [docs/DEVELOPPEMENT.md](docs/DEVELOPPEMENT.md), in French):
   `dotnet build MarkView.sln` must succeed **with no errors or warnings**, and `dotnet test MarkView.sln` must pass.
3. Follow the existing style: code and comments in English, UI text in French.
4. Open a pull request explaining **why** the change is needed.

By submitting a contribution, you agree that it is distributed under the project's license,
[PolyForm Noncommercial 1.0.0](LICENSE.md).
