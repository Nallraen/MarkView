<p align="center">
  <img src="images/markview-logo.png" alt="Logo MarkView" width="128">
</p>

<h1 align="center">MarkView</h1>

<p align="center">
  <strong>Un éditeur Markdown simple, gratuit et hors ligne pour Windows.</strong><br>
  Écrivez à gauche, voyez le résultat à droite. C'est tout.
</p>

<p align="center">
  <a href="https://github.com/Nallraen/MarkView/releases/latest"><img alt="Dernière version" src="https://img.shields.io/github/v/release/Nallraen/MarkView?label=version"></a>
  <img alt="Windows 10 et 11" src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4">
  <a href="LICENSE.md"><img alt="Licence : usage non commercial" src="https://img.shields.io/badge/licence-PolyForm%20Noncommercial-blue"></a>
</p>

<p align="center">
  🇫🇷 Français · <a href="README.en.md">🇬🇧 English</a>
</p>

---

![MarkView en thème clair](docs/images/screenshot-light.png)

## Pourquoi MarkView ?

J'avais besoin d'un outil **simple** pour lire et écrire mes fichiers Markdown (`.md`) sous Windows,
et je n'ai rien trouvé de **gratuit** qui me convienne : trop lourd, trop compliqué, payant ou dépendant
d'Internet. J'ai donc créé MarkView pour résoudre ce problème personnel, et je le partage pour celles et ceux
qui auraient le même besoin.

## Télécharger et lancer

1. Allez sur la page des **[versions (Releases)](https://github.com/Nallraen/MarkView/releases/latest)**.
2. Téléchargez **`MarkView.exe`** (ou directement : **[MarkView.exe](https://github.com/Nallraen/MarkView/releases/latest/download/MarkView.exe)**).
3. Double-cliquez dessus. C'est prêt : **aucune installation**, rien d'autre à télécharger.

Vous pouvez ranger `MarkView.exe` où vous voulez (Bureau, `Documents`, clé USB…).

> [!NOTE]
> **Windows affiche « Windows a protégé votre ordinateur » ?** C'est normal pour un petit logiciel gratuit
> qui n'a pas de certificat de signature payant. Cliquez sur **Informations complémentaires**, puis sur
> **Exécuter quand même**. Ce message n'apparaît qu'une fois.

<details>
<summary>Il existe aussi une version « légère » (3 Mo) : pour qui ?</summary>

`MarkView-light.exe` fait le même travail mais nécessite d'avoir installé le
[.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (« .NET Desktop Runtime », x64).
Si vous ne savez pas ce que c'est, prenez simplement `MarkView.exe`.
</details>

## Ce que fait MarkView

- ✍️ **Éditeur avec couleurs** : titres, gras, listes, code… sont colorés pendant que vous tapez.
- 👀 **Aperçu en direct**, mis à jour pendant la frappe, qui défile en même temps que le texte.
- 🖼️ **Images, tableaux, listes de tâches, notes de bas de page, blocs de code colorés**.
- 🗂️ **Plusieurs fichiers en onglets**. Ils sont rouverts au prochain lancement.
- 🧰 **Barre d'outils de mise en forme** : gras, italique, titres, listes, liens, images, tableaux…
- 🔎 **Rechercher / Remplacer**, avec options (majuscules, mot entier, expressions régulières).
- 🌗 **Thème clair, sombre ou automatique** (suit le réglage de Windows).
- 📤 **Export HTML et PDF, impression, copie en HTML** pour coller dans un e-mail ou un document.
- 🔒 **100 % hors ligne** : rien n'est envoyé sur Internet, vos fichiers restent chez vous.
- 💾 **Sûr** : avertit avant de fermer un fichier non enregistré et recharge un fichier modifié par un autre
  programme.

Trois façons d'afficher : **côte à côte** (`Ctrl+1`), **édition seule** (`Ctrl+2`) ou **lecture seule** (`Ctrl+3`).

![MarkView en thème sombre](docs/images/screenshot-dark.png)

## Ouvrir les fichiers `.md` avec MarkView par un double-clic

1. Faites un clic droit sur n'importe quel fichier `.md`, puis **Ouvrir avec** → **Choisir une autre application**.
2. Cliquez sur **Choisir une application sur votre PC** (ou « Rechercher une autre application »)
   et sélectionnez `MarkView.exe`.
3. Cochez **Toujours utiliser cette application**, puis validez.

Astuce : rangez d'abord `MarkView.exe` à son emplacement définitif. Si vous le déplacez ensuite, refaites
ces étapes.

## Raccourcis utiles

| Action | Raccourci |
|---|---|
| Nouveau / Ouvrir / Enregistrer | `Ctrl+N` / `Ctrl+O` / `Ctrl+S` |
| Gras / Italique / Lien | `Ctrl+B` / `Ctrl+I` / `Ctrl+K` |
| Titre 1, 2, 3 | `Ctrl+Alt+1`, `Ctrl+Alt+2`, `Ctrl+Alt+3` |
| Rechercher / Remplacer | `Ctrl+F` / `Ctrl+H` |
| Changer d'affichage | `Ctrl+1` / `Ctrl+2` / `Ctrl+3` |
| Zoom | `Ctrl+molette`, `Ctrl+0` pour revenir à 100 % |
| Imprimer | `Ctrl+P` |
| Plein écran | `F11` |

Tous les raccourcis sont indiqués dans les menus et dans les bulles d'aide des boutons.

## Questions fréquentes

**Est-ce que c'est vraiment gratuit ?**
Oui, pour un usage personnel ou non commercial (voir [Licence](#licence)).

**Ça marche sur Mac ou Linux ?**
Non, MarkView est conçu pour Windows 10 et 11 uniquement.

**L'aperçu affiche « Le runtime WebView2 est introuvable » ?**
L'aperçu utilise un composant de Windows (WebView2), présent d'office sur Windows 11 et sur la plupart des
Windows 10 à jour. Sinon, le message affiche un lien pour l'installer gratuitement. L'éditeur fonctionne
même sans lui.

**Où sont enregistrés mes réglages ? Comment désinstaller ?**
Les réglages sont dans `%AppData%\MarkView` et les fichiers temporaires dans `%LocalAppData%\MarkView`.
Pour désinstaller, supprimez `MarkView.exe` et ces deux dossiers. MarkView n'écrit rien d'autre sur votre PC.

**Comment voir tout ce qu'il sait afficher ?**
Ouvrez le fichier [`sample.md`](sample.md) de ce dépôt : il utilise toutes les syntaxes prises en charge.

## Contribuer

Les idées, signalements de bugs et propositions de modifications sont les bienvenus :

- un bug ou une idée ? Ouvrez une **[Issue](https://github.com/Nallraen/MarkView/issues)** ;
- envie de modifier le code ? Faites un **fork** et proposez une **pull request**, voir [CONTRIBUTING.md](CONTRIBUTING.md) ;
- documentation technique (compilation, architecture) : [docs/DEVELOPPEMENT.md](docs/DEVELOPPEMENT.md).

## Licence

MarkView est distribué sous licence **[PolyForm Noncommercial 1.0.0](LICENSE.md)**. En clair :

| ✅ Autorisé | ❌ Interdit |
|---|---|
| Utiliser MarkView gratuitement (particuliers, associations, écoles…) | **Toute utilisation commerciale** |
| Copier, partager, forker le projet | Vendre MarkView ou une version modifiée |
| Modifier le code et proposer des pull requests | Intégrer MarkView ou son code dans un produit ou service commercial |

Toute copie ou version modifiée doit conserver cette licence et la mention de l'auteur d'origine.
MarkView est fourni « tel quel », sans garantie.

MarkView s'appuie sur d'excellents projets libres (Markdig, AvalonEdit, highlight.js…), qui gardent leurs
propres licences : voir [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
