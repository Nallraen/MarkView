# Démonstration MarkView

![Logo MarkView](images/markview-logo.png)

Ce document montre **toutes les syntaxes** prises en charge par MarkView. Ouvrez-le en mode
*côte à côte* (`Ctrl+1`) et faites défiler l'éditeur : l'aperçu suit.

Aller directement à la section [Tableaux](#tableaux) ou aux [notes](#notes-de-bas-de-page).

---

## Mise en forme du texte

- **Gras** et __gras aussi__
- *Italique* et _italique aussi_
- ***Gras et italique***
- ~~Barré~~
- `code inline` et ``code avec un ` accent grave``
- Exposant : x^2^ · Indice : H~2~O
- ==Surligné== et ++inséré++
- Échappement : \*pas en italique\*

Un paragraphe avec un saut de ligne forcé  
juste ici (deux espaces en fin de ligne).

## Titres

### Titre de niveau 3

#### Titre de niveau 4

##### Titre de niveau 5

###### Titre de niveau 6

## Listes

### Liste à puces

- Premier élément
- Deuxième élément
  - Sous-élément A
  - Sous-élément B
    - Sous-sous-élément
- Troisième élément

### Liste numérotée

1. Préparer le café
2. Ouvrir MarkView
3. Écrire du Markdown
   1. Avec des sous-étapes
   2. Numérotées elles aussi

### Liste de tâches

- [x] Créer la solution .NET 10
- [x] Brancher Markdig et WebView2
- [ ] Conquérir le monde
- [ ] Tester `Entrée` dans une liste : la liste continue toute seule

## Citations

> Le Markdown est fait pour être lu tel quel, même sans être converti.
>
> > Une citation imbriquée.
>
> — *Quelqu'un de sensé*

## Code

Bloc C# :

```csharp
public sealed record Note(string Title, DateTime CreatedAt)
{
    public bool IsRecent => DateTime.Now - CreatedAt < TimeSpan.FromDays(7);
}

var notes = new List<Note> { new("Courses", DateTime.Now) };
Console.WriteLine(notes.Count(n => n.IsRecent));
```

Bloc Python :

```python
from pathlib import Path

def count_words(path: Path) -> int:
    """Compte les mots d'un fichier Markdown."""
    return len(path.read_text(encoding="utf-8").split())

print(count_words(Path("sample.md")))
```

Bloc JavaScript :

```javascript
const debounce = (fn, ms) => {
  let timer;
  return (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), ms);
  };
};
```

Bloc JSON :

```json
{
  "theme": "System",
  "editorFontSize": 14,
  "syncScroll": true
}
```

Bloc PowerShell :

```powershell
Get-ChildItem -Filter *.md -Recurse |
    Where-Object Length -gt 1KB |
    Select-Object Name, Length
```

Bloc sans langage :

```
Texte brut, sans coloration.
```

Bloc indenté (quatre espaces) :

    ligne indentée 1
    ligne indentée 2

## Tableaux

| Fonction       | Raccourci        | Alignement |
|:---------------|:----------------:|-----------:|
| Gras           | `Ctrl+B`         |        100 |
| Italique       | `Ctrl+I`         |         20 |
| Lien           | `Ctrl+K`         |          3 |
| Plein écran    | `F11`            |     12 345 |

Tableau en grille :

+---------+---------+
| Colonne | Valeur  |
+=========+=========+
| A       | 1       |
+---------+---------+
| B       | 2       |
+---------+---------+

## Liens

- Lien externe (s'ouvre dans le navigateur) : [Markdig sur GitHub](https://github.com/xoofx/markdig)
- Lien automatique : https://learn.microsoft.com/microsoft-edge/webview2/
- Lien vers un autre fichier Markdown (s'ouvre dans un nouvel onglet) : [README](README.md)
- Lien interne : [retour en haut](#démonstration-markview)
- Courriel : <contact@example.com>

## Images

Image relative, résolue par rapport au dossier de ce fichier :

![Logo MarkView, petit](images/markview-logo.png "MarkView")

## Notes de bas de page

MarkView utilise Markdig[^1] pour la conversion et WebView2[^webview] pour l'aperçu.

[^1]: Markdig est un processeur Markdown rapide et extensible pour .NET.
[^webview]: Le moteur Chromium d'Edge, intégré à Windows.

## Listes de définitions

Markdown
:   Langage de balisage léger créé en 2004.

MarkView
:   Éditeur et visualiseur Markdown pour Windows.

## Abréviations

*[HTML]: HyperText Markup Language

Le HTML est généré à partir du Markdown.

## HTML brut

<details>
<summary>Cliquer pour déplier</summary>

Le HTML brut est autorisé, mais aucun script injecté ne s'exécute :

<img src="nexiste-pas.png" onerror="alert('ce script est bloqué')" alt="image manquante (script bloqué)">

</details>

<kbd>Ctrl</kbd> + <kbd>S</kbd> pour enregistrer.

## Ligne horizontale

Au-dessus.

***

En dessous.

## Texte long pour tester le défilement synchronisé

Lorem ipsum dolor sit amet, consectetur adipiscing elit. Integer nec odio. Praesent libero.
Sed cursus ante dapibus diam. Sed nisi. Nulla quis sem at nibh elementum imperdiet.

Duis sagittis ipsum. Praesent mauris. Fusce nec tellus sed augue semper porta. Mauris massa.
Vestibulum lacinia arcu eget nulla. Class aptent taciti sociosqu ad litora torquent per conubia nostra.

Curabitur sodales ligula in libero. Sed dignissim lacinia nunc. Curabitur tortor. Pellentesque nibh.
Aenean quam. In scelerisque sem at dolor. Maecenas mattis. Sed convallis tristique sem.

Proin ut ligula vel nunc egestas porttitor. Morbi lectus risus, iaculis vel, suscipit quis, luctus non, massa.
Fusce ac turpis quis ligula lacinia aliquet. Mauris ipsum. Nulla metus metus, ullamcorper vel, tincidunt sed, euismod in, nibh.

### Fin

Merci d'avoir essayé **MarkView** !
