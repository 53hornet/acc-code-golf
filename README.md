# ACC hosts a code golf

Actually, it's chess.

## Get some data

Grab [this archive](https://azarahealthcare-my.sharepoint.com/:u:/g/personal/adam_carpenter_azara_healthcare/EcHEHU8TbgJCt2YXEF3Ig20BrY5IudPTblhTrUYOrFyXCw?e=jfkOaS) and [extract it](https://man.freebsd.org/cgi/man.cgi?tar(1)) to get the input file `mega-clean.pgn`. Uncompressed the thing is larger than 4GB because it contains millions of chess game results. The entries are in [PGN format](https://en.wikipedia.org/wiki/Portable_Game_Notation), which looks like this.

```
[Event "BCF-ch 33rd"]
[Site "Nottingham"]
[Date "1946.08.12"]
[Round "1"]
[White "Abrahams, Gerald"]
[Black "Parr, Frank"]
[Result "0-1"]
[ECO "D31"]
[EventDate "1946.08.12"]
[PlyCount "74"]
...
```

Here's a sample of just the results of a few games.

```
[Result "0-1"]
[Result "0-1"]
[Result "1-0"]
[Result "1-0"]
[Result "1/2-1/2"]
[Result "1-0"]
[Result "0-1"]
[Result "1-0"]
[Result "0-1"]
[Result "0-1"]
```

These values have these meanings.

- `1-0` white won
- `0-1` black won
- `1/2-1/2` draw

## The challenge

We want to summarize the results of all of the games in this file. That means a breakdown of how many times black and white won. We also want to know how many games ended in a stalemate (draw). Finally, we want the total number of games (the sum of black wins, white wins, and draws).

Here's the answer. To be correct, your solution should produce results which look like this.

| total | white | black | draw |
| --- | --- | --- | --- |
|  5590058 | 2253058 | 1810804 | 1526196 |

Just to be clear, this means there were 2253058 instances of `1-0` and 1810804 instances of `0-1`. 1526196 is how many instances of `1/2-1/2` were in there, and the total is all three of those numbers summed.

Now you just need to make the solver. Write a tool which creates these calculations using the same input file. Your solution should not just regurgitate these numbers :)

## Additional criteria

- _You must work together!_ Pair programming in teams of two. Talk about the problem together, code together, present your solution together.
- You may use any language, framework, library, etc. 
- You may use AI tools, but tell us so we can see how you used them effectively!
- Everyone has the same amount of time: the end of the summit on Wednesday.

## Submitting a solution

1. Check out this repository and make a branch or fork
2. Add all of your code but no third party code, e.g., `node_modules`. Put that stuff in .gitignore if you want to.
3. Write the game results table output to the CONTRIBUTING.md. Include timings results from [`time(1)`](https://man.freebsd.org/cgi/man.cgi?time) or [`Measure-Command`](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.utility/measure-command?view=powershell-7.5).
4. Create a PR against this repository with both your names in the title

We'll use Github to measure lines of code and languages used.

## Judging and winning

The good news is there could be three winners! You will be judged on your solution in three categories:

1. Speed (fastest solution wins)
2. Size (smallest code wins)
3. Style (people's choice)

To win, your solution must produce the correct results and satisfy at least one of the above categories. If your solution runs faster than everyone else's, then you win for speed! If you happened to use the fewest characters to do it, then you win for size! If you somehow managed to do both, then you win in both categories!

People's choice will be put to a vote at the end of the competition. Everyone gets to pick their favorite solution, completely subjective.

The winners will be announced EOD Wednesday.

## Get to it! 

Pair up, grab the data, and get coding!
