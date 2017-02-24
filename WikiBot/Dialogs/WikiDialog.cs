using Microsoft.Bot.Builder.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading.Tasks;
using Microsoft.Bot.Connector;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;

namespace WikiBot.Dialogs
{
    public class WikiDialog : IDialog<object>
    {
        public async Task StartAsync(IDialogContext context)
        {
            //await context.PostAsync("Hi, I'm your friendly neighborhood WikiBot. What would you like to search for?");
            context.Wait(MessageReceivedAsync);
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> argument)
        {
            var message = await argument;

            if (message.Text.ToLower() == "yes" || message.Text.ToLower() == "sure" || message.Text.ToLower() == "more")
            {
                string wikiResult = context.ConversationData.Get<string>("wikiresults");
                int startIndex = context.ConversationData.Get<int>("wikiindex");
                startIndex += 5;
                context.ConversationData.SetValue<int>("wikiindex", startIndex);

                await FillMessageCards(context, message.Text, wikiResult, startIndex);
            }
            else
            {
                await context.PostAsync("Querying Wikipedia on the topic **'" + message.Text + "'**");

                string wikiResult = await SearchWikipedia(message.Text);
                context.ConversationData.SetValue<string>("wikiresults", wikiResult);
                int startIndex = 0;
                context.ConversationData.SetValue<int>("wikiindex", startIndex);

                bool result = await FillMessageCards(context, message.Text, wikiResult, startIndex);

                

                //else
                //    await context.PostAsync("Sorry, nothing found on the topic **'" + message.Text + "'**");

            }
        }

        private async Task<bool> FillMessageCards(IDialogContext context, string searchTerm, string wikiResult, int startIndex)
        {
            bool result = false;
            Newtonsoft.Json.Linq.JArray jsonResult = JArray.Parse(wikiResult);

            if (jsonResult.Count == 4 && startIndex < ((JArray)jsonResult[1]).Count)
            {
                result = true;
                IMessageActivity msg = context.MakeMessage();
                msg.Attachments = new List<Attachment>();
                msg.AttachmentLayout = "carousel";
                msg.Text = "Here's some info for ya..";

                JArray titleArray = (JArray)jsonResult[1];
                JArray descriptionArray = (JArray)jsonResult[2];
                JArray linkArray = (JArray)jsonResult[3];

                int count = startIndex + 5;
                if (count > titleArray.Count) count = titleArray.Count;

                for (int i = startIndex; i < count; i++)
                {
                    //Account for empty descriptions
                    if (descriptionArray[i].ToString() == "") descriptionArray[i] = "No description available!";

                    List<CardImage> cardImages = new List<CardImage>();
                    // Get article image
                    string imagePayload = await GetWikipediaPicture(titleArray[i].ToString());
                    cardImages.Add(new CardImage(url: imagePayload));

                    List<CardAction> cardButtons = new List<CardAction>();

                    CardAction plButton = new CardAction()
                    {
                        Value = linkArray[i].ToString(),
                        Type = "openUrl",
                        Title = "Open Wikipedia Page"
                    };
                    cardButtons.Add(plButton);

                    HeroCard plCard = new HeroCard()
                    {
                        Title = titleArray[i].ToString(),
                        Subtitle = descriptionArray[i].ToString(),
                        Images = cardImages,
                        Buttons = cardButtons
                    };

                    msg.Attachments.Add(plCard.ToAttachment());
                }

                await context.PostAsync(msg);

                if (count < titleArray.Count)
                {
                    await context.PostAsync("Would you like to see more results?");
                }
            }
            else
            {
                await context.PostAsync("Sorry, all out of results today..  What else would you like to search for?");
            }

            return result;
        }

        private async Task<string> SearchWikipedia(string message)
        {
            string result = "";
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri("https://en.wikipedia.org/w/api.php?action=opensearch&format=json&search=" + message);

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await client.GetAsync("");
            if (response.IsSuccessStatusCode)
            {
                result = await response.Content.ReadAsStringAsync();
            }

            return result;
        }

        private async Task<string> GetWikipediaPicture(string Title)
        {
            string result = "";
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri("http://en.wikipedia.org/w/api.php?action=query&titles=" + Title + "&prop=pageimages&format=json&pithumbsize=300");

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await client.GetAsync("");
            if (response.IsSuccessStatusCode)
            {
                result = await response.Content.ReadAsStringAsync();
            }

            Newtonsoft.Json.Linq.JObject jsonResult = JObject.Parse(result);
            if (jsonResult != null)
            {
                //result = jsonResult["query"]["pages"].Children[0]["thumbnail"]["source"].ToString();
                JToken thumbnailObject = jsonResult["query"]["pages"].FirstOrDefault().FirstOrDefault()["thumbnail"];
                if (thumbnailObject != null)
                    result = thumbnailObject["source"].ToString();
                else
                    result = "";
            }

            return result;
        }
    }
}