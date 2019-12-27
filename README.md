# HelpJuice to Markdown

A utility to convert HelpJuice categories, questions and answers into Markdown files. Given your API key as a user secret, the program will convert:

1. Your HelpJuice categories to directories (and sub-directories as related)
2. Your HelpJuice questions to directories (saved within the related directory)
3. Your HelpJuice answers from HTML to Markdown (as a `README.md` within the related question directory)

Currently program requires that you complete a full conversion; categories, questions and answers must be processed in that order.

## Feature list

- Question tags are inserted into answer files as VuePress tags
- Includes a link to old document, original question ID, created date/time, updated date/time, and accessibility setting within the answer file
- Images are downloaded from HelpJuice and added as local assets within the related question directory
- If the image was downloaded, the `<img src>` is updated appropriately before the HTML to Markdown conversion
- Outputs failed images to text doc (`Images.txt`)
- Forces lower case for all folders and files
- Links are tracked and converted as able
- Outputs failed/skipped link conversions to text doc (`Links.txt`)
- Builds a README.md for each directory that links to sub-directories and articles by default

## Dependencies

1. API keys are managed with a [user secrets file](https://www.twilio.com/blog/2018/05/user-secrets-in-a-net-core-console-app.html)
2. The Answers API returns CSV files so [CsvHelper](https://joshclose.github.io/CsvHelper/) is used to parse the output
3. Answers are stored in HTML so [ReverseMarkdown-Net](https://github.com/mysticmind/reversemarkdown-net) is used to convert them to Markdown
