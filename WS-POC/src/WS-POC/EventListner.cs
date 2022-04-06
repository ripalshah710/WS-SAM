using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;

using Amazon.Lambda.Core;
using Amazon.Lambda.CloudWatchEvents;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace WS_POC
{
    public class EventListner
    {
        
        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<string> EventListnerHandler(CloudWatchEvent<dynamic> evnt, ILambdaContext context)
        {
            // All log statements are written to CloudWatch by default. For more information, see
            // https://docs.aws.amazon.com/lambda/latest/dg/nodejs-prog-model-logging.html
            context.Logger.LogLine(JsonSerializer.Serialize(evnt));
            await SendMessage(context);
            return "Done";
        }

        private async Task SendMessage(ILambdaContext context)
        {
            try
            {
                var snsClient = new AmazonSimpleNotificationServiceClient(Amazon.RegionEndpoint.USEast1);
                var request = new PublishRequest
                {
                    TopicArn = SNSTopic,
                    Message = "NotifyFaci",
                    Subject = "NotifyFaci"

                };
                await snsClient.PublishAsync(request);
            }
            catch(Exception ex)
            {
                context.Logger.LogLine("Error in Push data to topic : " + ex.Message);
            }
        }

        private string SNSTopic
        {
            get
            {
                return Environment.GetEnvironmentVariable("SNS_Topic");
            }
        }
    }
}
