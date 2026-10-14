### PHASE TWO IS LIVE! SEE BELOW!

# ACC hosts a code golf (again)

You're an artist, Bob.

## Get some data

Check out this repo and look at [azara.pgm](./azara.pgm). It's a plain PNM image format, in grayscale.

Here's a conversion of the image so you can preview it in Github.
![azara.pnm](./ignore-me-just-a-preview.png)

Here's the actual contents of the image file, shortened.
```azara.pgm
P2
# Azara Healthcare, LLC
792 404
255
0 0 0 0 0 0 0 0 0 0 0 ...
```

You can [read more about this format and how it works here](https://netpbm.sourceforge.net/doc/pgm.html), as it'll be relevant to this challenge.
Here's the TL;DR on this format.
1. It's all plaintext
2. Comments start with `#` and are ignored
3. The first three lines set the format: a magic number, dimensions, and max color
4. A matrix of pixels composing the image

## The challenge

We want to compress this image. That means shrink it down into a smaller size on disk. The compressed image does not need to be viewable, but if we de-compress it, we should be able to view it again. The resulting de-compressed image must look the same as the raw image.

To be correct, your solution should produce a compressed image file which, when de-compressed using your same solution, looks the same as the original image provided. 

_Just to be clear, this does not mean the contents of your file must be identical to the raw, just that your image is visually the same. This will be mathematically checked :)_

You already have the solution: it's the same image you were given, just make it smaller.

## Additional criteria

- _You must work together!_ Pair programming in teams of two. Talk about the problem together, code together, present your solution together.
- You may use any language, framework, library, etc. _however_ you must write the compression algorithm. No `tar`, no `gzip`, no `.compress()`
- You may use AI tools, but tell us so we can see how you used them effectively!
- Everyone has the same amount of time: the end of the summit on Thursday.

## Testing

ImageMagick will be used to verify your output image's correctness. It has a [compare command which visually checks two images](https://imagemagick.org/compare). I verified this works for this scenario, regardless of the file's contents.

You can test your own image like this.
```sh
$ magick compare -metric rmse azara.pgm azara.final.pgm compare.gif
0 (0)
```

The output should stay 0. If it's any other number, check `compare.gif` to see what's corrupted. If you want to view your PGM image, try the ImageMagick `display` command or use the [recommended VS Code extension](https://marketplace.visualstudio.com/items?itemName=ngtystr.ppm-pgm-viewer-for-vscode).

## Submitting a solution

1. Check out this repository and make a branch or fork
2. Add all of your code but no third party code, e.g., `node_modules`. Put that stuff in .gitignore if you want to.
3. Write the compressed image to `azara.compressed.pgm` and the de-compressed resulting image to `azara.final.pgm`. Check those into the repo so we can see!
4. Create a PR against this repository with both your names in the title

## Part 2

### THERE IS A PART 2.

You'll now find [azara.ppm](./azara.ppm) in this repo. It's a slightly different format, the plaintext color flavor. Your solution must also compress and decompress this image using the same commands as your first one. 

Those who thought through a future-proof design probably don't need to make any changes. You will need to re-submit your solution if it doesn't work with the edge case.

## Judging and winning

The good news is there could be three winners! You will be judged on your solution in three categories:

1. Lines of code (smallest source file wins)
2. Compression ratio (smallest compressed image wins)
3. Style (people's choice)

To win, your solution must produce the correct results and satisfy at least one of the above categories. If your solution makes a smaller image than everyone else, then you win for ratio! If you happened to use the fewest characters to do it, then you win for size! If you somehow managed to do both, then you win in both categories!

People's choice will be put to a vote at the end of the competition. Everyone gets to pick their favorite solution, completely subjective.

The winners will be announced Thursday afternoon.

## Get to it! 

Pair up, grab the paintbrush, and get coding!
