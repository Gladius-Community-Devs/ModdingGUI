# Generated

`index.html`, `catalog.json`, `engine-methods.json` and `engine-only.json` are produced by `scripts/build-script-api.py` (with
`scripts/extract-engine-methods.py` feeding the DOL half). Do not hand-edit them: regenerate, or edit the script and the corpus
that feeds it.

The page is a self-contained browse of the game's scripting surface - every interface, method and object the scripts use, plus
the engine-side methods recovered from `bootfile.dol` that no script calls yet.