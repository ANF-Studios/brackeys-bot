﻿using System;
using System.Linq;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;

namespace BrackeysBot.Commands
{
	public partial class UtilityModule : BrackeysBotModule
	{
		readonly string[] languages = new string[]
		{ "actionscript", "angelscript", "arcade", "arduino", "aspectj", "autohotkey", "autoit", "cal", "capnproto", "ceylon", "clean", "coffeescript", "cpp", "crystal", "c", "cs", "csharp", "css", "d", "dart", "diff", "dos", "dts", "glsl", "gml", "go", "gradle", "groovy", "haxe", "hsp", "http", "java", "js", "json", "kotlin", "leaf", "less", "lisp", "livescript", "lsl", "lua", "mathematica", "matlab", "mel", "perl", "n1ql", "nginx", "nix", "objectivec", "openscad", "php", "powershell", "processing", "protobuff", "puppet", "qml", "r", "reasonml", "roboconf", "rsl", "rust", "scala", "scss", "sql", "stan", "swift", "tcl", "thrift", "typescript", "vala", "zephir" };

		[Command("formatcode"), Alias("code", "codify", "format")]
		[Summary("Turns inputted code into a formatted code block and pastes it into the channel.")]
		[Remarks("format [language] <id | code>")]
		public async Task FormatCodeAsync([Summary("ID of a message, or pasted code."), Remainder] string input)
		{
			if (FilterService.ContainsBlockedWord(input))
			{
				return;
			}
			
			var userMention = $"<@{Context.User.Id}>";
			var firstWord = input.Split(" ")[0];

			// If a language is entered, remove it from input
			if (TryDetectLanguage(firstWord, out string language))
			{
				input = input.Remove(0, language.Length + 1);	// Length + 1 for removing the whitespace as well, faster than calling .Trim();
				firstWord = input.Split(" ")[0];
			}

			// If an id is entered, try assigning the target message's content to input
			if (TryDetectMessageFromId(firstWord, out string content, out ulong messageId, out ulong messageAuthorId))
			{
				input = RemoveBacktics(content);
				var user = await Context.Guild.GetUserAsync(messageAuthorId);
				userMention = $"{user.Username}";
				if (CanDeleteOriginal(messageId))
				{
					await Context.Channel.DeleteMessageAsync(messageId);
				}				
			}
			else
			{
				await Context.Message.DeleteAsync();
			}

			var trimmedCode = RemoveEmptyMethods(input);
			var formattedCode = FormatCodeService.Format(trimmedCode);

			await Context.Channel.SendMessageAsync($"Codeblock pasted by {userMention}:\n```{language}\n{formattedCode}\n```");
		}
		
		private bool CanDeleteOriginal(ulong id)
		{
			var isGuruOrAbove = (Context.User as IGuildUser).GetPermissionLevel(Context) >= PermissionLevel.Guru;

			var timePassed =  DateTimeOffset.Now - Context.Channel.GetMessageAsync(id).Result.CreatedAt;

			return isGuruOrAbove && timePassed.TotalMilliseconds < FormatCodeService.GetTimeoutSetting();
		}

		// Try detecting a language, default to cs if no language is found
		private bool TryDetectLanguage(string input, out string language)
		{
			if (languages.Contains(input))
			{
				language = input;
				return true;
			}
			language = "cs";
			return false;
		}

		// Try detecting a message from the given id
		private bool TryDetectMessageFromId(string input, out string content, out ulong messageId, out ulong messageAuthorId)
		{
			if (ulong.TryParse(input, out ulong id))
			{
				var targetMessage = Context.Channel.GetMessageAsync(id).Result;
				if (targetMessage != null)
				{
					messageId = id;
					messageAuthorId = targetMessage.Author.Id;
					content = targetMessage.Content;
					return true;
				}							
			}
			messageId = 0;
			messageAuthorId = 0;
			content = string.Empty;
			return false;
		}

		// Remove backticks and highlighting if the message copied from id has any
		private string RemoveBacktics(string input)
		{	
			if (input.StartsWith("```"))
			{
				input = input.Remove(0, 3);
				if (TryDetectLanguage(input.Split('\n')[0], out string language))
				{
					input = input.Remove(0, language.Length);
					var charArray = input.ToCharArray();
					for (int charIndex = 0; charIndex < charArray.Length; charIndex++)
					{
						if (charArray[charIndex] == '`')
						{					
							charArray[charIndex] = ' ';						
						}
					}
					input = new string(charArray);
				}			
			}			
			return input;
		}

		// Removes empty void methods like the default Update() and Start() that some people can't be bothered to delete themselves
		private static string RemoveEmptyMethods(string remainingText)
		{
			var codeLines = remainingText.Split('\n').ToList();

			var isChecking = false;
			var startIndex = 0;
			int startComment = -1;
			int updateComment = -1;

			// Iterate over each line
			for (int lineIndex = 0; lineIndex < codeLines.Count; lineIndex++)
			{
				// Save comment line indices
				if (codeLines[lineIndex].Contains("// Start is called before the first frame update"))
				{
					startComment = lineIndex;
				}
				else if (codeLines[lineIndex].Contains("// Update is called once per frame"))
				{
					updateComment = lineIndex;
				}

				// Start checking starting from next line
				if (codeLines[lineIndex].Contains("void"))
				{
					isChecking = true;
					startIndex = lineIndex;
				}

				// Delete the method and comment lines if nothing other than declaration and body brackeys are found
				else if (isChecking && codeLines[lineIndex].Contains("}"))
				{
					isChecking = false;

					if (startComment != -1)
					{
						codeLines[startComment] = codeLines[startComment].Replace("// Start is called before the first frame update", string.Empty);
					}
					if (updateComment != -1)
					{
						codeLines[updateComment] = codeLines[updateComment].Replace("// Update is called once per frame", string.Empty);
					}

					for (int i = startIndex; i < lineIndex + 1; i++)
					{
						codeLines.RemoveAt(startIndex);
					}
					updateComment = -1;
					startComment = -1;
					startIndex = 0;
					lineIndex = 0;
				}

				// Stop checking if method isn't empty
				else if (isChecking && !string.IsNullOrWhiteSpace(codeLines[lineIndex]) && !codeLines[lineIndex].Contains("{"))
				{
					isChecking = false;
					updateComment = -1;
					startComment = -1;
					startIndex = 0;
				}
			}

			return string.Join('\n', codeLines);
		}
	}
}