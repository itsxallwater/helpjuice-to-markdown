# HelpJuice to Markdown

A utility to convert HelpJuice categories, questions and answers into Markdown files. Given your API key as a user secret, the program will convert:

1. Your HelpJuice categories to directories (and sub-directories as related)
2. Your HelpJuice questions to Markdown files (saved within the related directory)
3. Your HelpJuice answers from HTML to Markdown (within the related Markdown file)

Currently program requires that you complete a full conversion; categories, questions and answers must be processed in that order.

## TODO / Feature list

- [x] At this point, we can hit the Zumasys and jBASE sites and retrieve Categories and Questions
- [x] Categories should be turned into File Folders (start in Temp?)
- [x] Questions should be turned into empty Markdown files in the appropriate folder
- [x] Need to paginate through the API to return all questions
- [x] Should display some job/work status to the console now
- [x] Refactor!!
- [x] Insert question tags into files
- [x] Answers can then be retrieved but the down side is they are returned as CSV or XLS
- [x] Will need to add a CSV parser to turn the download into a List\<Answer\>
- [x] Once parsed, will need an HTML -> Markdown converter to convert and save the output to the appropriate file
- [x] Images should be extracted from HelpJuice and added as local assets, after which the links should be updated
- [x] Blob images not working correctly, not all docs rendering correctly
- [x] Convert articles as files to folders w/ content in README.md & related images saved within
- [x] Lower case folders and files
- [ ] Links need to be tracked and converted
- [x] Include link to old document for good measure
- [ ] Build README.md for each directory that links to sub-directories and articles

## Dependencies

1. API keys are managed with a [user secrets file](https://www.twilio.com/blog/2018/05/user-secrets-in-a-net-core-console-app.html)
2. The Answers API returns CSV files so [CsvHelper](https://joshclose.github.io/CsvHelper/) is used to parse the output
3. Answers are stored in HTML so [ReverseMarkdown-Net](https://github.com/mysticmind/reversemarkdown-net) is used to convert them to Markdown
