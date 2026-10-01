# Substitution catalogues

Glasses the optimiser's basin hopping may substitute IN, and nothing else.

This folder is deliberately separate from `catalogs/Glass`. Those are for READING a design: a
lens file names a glass and the index has to be resolved, so every vendor catalogue has to be
there or the answer is silently wrong. These are for CHOOSING a glass, which is a different
question with a different answer - a search that may pick anything from every vendor's full
catalogue will wander into glasses nobody stocks, and the design that comes back cannot be built.

`CoreSet28` is a twenty-eight glass working set. Name it with `--glass_substitution CoreSet28`.
