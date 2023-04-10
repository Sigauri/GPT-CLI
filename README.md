# GPT-CLI: Command Line Interface for OpenAI's GPT

Welcome to GPT-CLI, a command line interface for harnessing the power of OpenAI's GPT, the world's most advanced language model. This project aims to make the incredible capabilities of GPT more accessible and easily integrated into your workflow, right from the command line.

## Prerequisites

In order to use this CLI interface, you will need an OpenAI API key. [Sign up](https://platform.openai.com) if you haven't already, and create an [api key](https://platform.openai.com/account/api-keys). 

Then, create an appSettings.json file in the current working directory with the following contents:

```json
{
  "OpenAI": {
    "api-key": "sk-your-api-key-here"
  }
}
```

Alternatively, you can also use the api-key command line parameter:

```bash
gpt api-key "sk-your-api-key-here" prompt "generate a hello world python script" > hello.py
```

Since this is a .NET 7 standalone console application, you won't need to worry about installing the .NET CLI or runtime in your environment.

## Features

GPT, or Generative Pre-trained Transformer, is known for its remarkable language understanding and generation abilities. Here are some features that you might have already experienced using OpenAI's GPT model, whether it's through the API or ChatGPT:

- **Text Completion:** GPT can automatically complete text based on a given prompt, making it great for drafting emails, creating content, or even generating code.
- **Conversational AI:** GPT's ability to understand context and maintain a conversation makes it ideal for building chatbots, AI assistants, and customer support solutions.
- **Translation:** Leverage GPT's multilingual capabilities to translate text between languages quickly and accurately.
- **Summarization:** GPT can summarize long pieces of text, extracting key points and condensing information into a more digestible format.
- **Question-Answering:** GPT can be used to build powerful knowledge bases, capable of answering questions based on context and provided information.

## Project Intent

The primary goal of this project is to create a command line interface (CLI) for GPT, allowing you to access its incredible capabilities right from your terminal. This CLI will enable you to perform tasks like generating code, writing content, and more, all by simply running commands.

For example, you could generate a bash script for "Hello, World!" with the following command:
```bash
gpt --prompt "create a bash Hello World script" > hello.sh
```

But GPT-CLI's potential doesn't stop there. You can even pipe GPT commands to other commands, or back to GPT itself, creating powerful and dynamic workflows.

Here are some additional ideas for GPT-CLI functionalities:

1. **Code Refactoring:** Use GPT-CLI to refactor your code by providing a prompt and outputting the result to the desired file.
```bash
cat original_code.py | gpt --prompt "refactor this Python function for better readability" > refactored_code.py
```

2. **Automated Documentation:** Generate documentation for your code by providing relevant prompts.
```bash
gpt --prompt "create markdown documentation for this JavaScript code" < file.js
```

3. **Text Processing:** Use GPT-CLI in conjunction with other command line tools to process and manipulate text, such as grep, awk, or sed.
```bash
curl -s https://datti.net/2023/03/14/Publishing-an-Azure-Static-Website-with-Github-Actions-&-Jekyll/ | grep -zPo '<section id="content" class="main inactive">\K.*?(?=</section>)' | sed 's/<[^>]*>//g' | gpt --prompt "summarize this article" | grep 'keyword' > summarized_with_keyword.txt
```

4. **Combine Text from Multiple Files and Summarize:**
```bash
cat file1.txt file2.txt | gpt --prompt "combine and summarize the information from these two texts" > summarized_information.txt
```

5. **Generate a List of Ideas and Sort by Relevance:**
```bash
gpt --prompt "generate a list of 10 innovative AI project ideas" | sort -R | gpt --prompt="rank these AI project ideas by their potential impact" > sorted_AI_project_ideas.txt
```

6. **Extract Quotes from a Text and Generate a Motivational Poster:**
```bash
grep -o '".*"' input_text.txt | gpt --prompt "create a motivational poster using one of these quotes" > motivational_poster.txt
```

7. **Filter Log File and Generate a Report:**
```bash
grep 'ERROR' log_file.txt | gpt --prompt "analyze these error logs and generate a brief report on the most common issues" > error_report.txt
```

8. **Embed context into a chat session via curl, stripping html tags with sed:**
```bash
curl http://url/documentation | sed 's/<[^>]\+>//g' | gpt embed --chunk-size=2048 > docs.dat && gpt chat --file docs.dat
```
With GPT-CLI, the possibilities are limited only by your imagination and ability to prompt and string together commands.

I hope this tool will empower you to integrate GPT into your workflow, streamline your tasks, and unleash your creativity.
