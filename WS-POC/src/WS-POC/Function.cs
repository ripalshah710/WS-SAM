using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.SNSEvents;
using Amazon.SQS;
using Amazon.SQS.Model;
using Newtonsoft.Json;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace WS_POC
{
    public class Function
    {

        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
        /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
        /// region the Lambda function is executed in.
        /// </summary>
        public Function()
        {

        }


        /// <summary>
        /// This method is called for every Lambda invocation. This method takes in an SNS event object and can be used 
        /// to respond to SNS messages.
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<string> FunctionHandler(SNSEvent evnt, ILambdaContext context)
        {
            if(evnt != null & evnt.Records.Count > 0)
            {
                await ProcessRecordAsync(evnt.Records[0], context);
            }

            return "done";
        }

        private async Task ProcessRecordAsync(SNSEvent.SNSRecord record, ILambdaContext context)
        {
            // All log statements are written to CloudWatch by default. For more information, see
            // https://docs.aws.amazon.com/lambda/latest/dg/nodejs-prog-model-logging.html
            context.Logger.LogLine($"Processed record {record.Sns.Message}");
            Models.Message.MessageType messageType;
            Enum.TryParse<Models.Message.MessageType>(record.Sns.Message, out messageType);
            string procedureName = string.Empty;
            switch (messageType)
            {
                 case Models.Message.MessageType.NotifyFaci:
                    procedureName = "[/sysmail/Notification/notifyTofaci]";
                    break;
               /* case Models.Message.MessageType.ClockHour:
                    procedureName = "[/sysmail/Notification/clockhourapproved]";
                    break;
                case Models.Message.MessageType.ClockHourDenial:
                    procedureName = "[/sysmail/Notification/clockhourdenial]";
                    break;
                case Models.Message.MessageType.ClockHourResubmit:
                    procedureName = "[/sysmail/wa_esd113Notification/clockhourresubmit]";
                    break;
                case Models.Message.MessageType.DistrictContact:
                    procedureName = "[/sysmail/Notification/districtcontact]";
                    break;
                case Models.Message.MessageType.EventApproved:
                    procedureName = "[/sysmail/Notification/eventapproval]";
                    break;
                case Models.Message.MessageType.EventCreated:
                    procedureName = "[/sysmail/Notification/eventcreation]";
                    break;
                case Models.Message.MessageType.EventSchedule:
                    procedureName = "[/sysmail/Notification/eventSchedule]";
                    break;
                case Models.Message.MessageType.MasterEventCreated:
                    procedureName = "[/sysmail/Notification/mastereventcreation]";
                    break;
               
                case Models.Message.MessageType.RequestClockHour:
                    procedureName = "[/sysmail/Notification/clockhourrequest]";
                    break;
                case Models.Message.MessageType.SessionRegistration:
                    procedureName = "[/sysmail/Notification/registrationconfirmation]";
                    break;*/
            }

            if (procedureName != string.Empty)
            {
                context.Logger.LogLine($"Procedure Name found {procedureName}");
                await GetAndPushData(procedureName, context);
            }
            
            // TODO: Do interesting work based on the new message
            await Task.CompletedTask;
        }

        private async Task GetAndPushData(string procedureName, ILambdaContext context)
        {
            using (SqlCommand SQLCommand = new SqlCommand(procedureName, new SqlConnection(this.ConnectionString)))
            {
                SQLCommand.CommandType = CommandType.StoredProcedure;
                
                try
                {
                    // string json = "{\"attendee_pk\":2586684,\"title\":\"1643799-Exploring the K-12 Science Framework  (Canvas)\",\"schedule\":\"Mar 08, 2022-May 07, 2022\",\"emails\":\"dodie.resendez@esc4.net\",\"location\":\"Region 4 ESC Online Course\",\"name\":\"Support Account\",\"email\":\"helpdesk@esc4.net\",\"Address1\":\"7145 W. TIDWELL\",\"Address2\":\"Humble,TX 77092\",\"home_phone\":\"8006951825\",\"work_phone\":\"7137446808\",\"Room\":\"Region 4 ESC,ESC Staff\"}";
                    // await PushMessagetoSQS(json, context);
                    context.Logger.LogLine($"Trying for DB Connection");
                    SQLCommand.Connection.Open();
                    context.Logger.LogLine($"DB connection open");
                    SqlDataReader dr = SQLCommand.ExecuteReader(CommandBehavior.CloseConnection);
                    
                    while (dr.Read())
                    {
                        await PushMessagetoSQS(Convert_DataRowToJson(dr), context);
                    }
                    context.Logger.LogLine($"All messages pushed successfully");
                }
                catch (Exception ex)
                {
                    context.Logger.LogLine($"Error in executing command {ex.Message}");
                }

                finally
                {
                    if (SQLCommand.Connection.State != ConnectionState.Closed)
                        SQLCommand.Connection.Close();
                }
            }
        }

        private string Convert_DataRowToJson(SqlDataReader datarow)
        {
            var dict = new Dictionary<string, object>();
            for (int i = 0; i < datarow.FieldCount; i++)
            {
                dict.Add(datarow.GetName(i), datarow[i]);
            }

            return JsonConvert.SerializeObject(dict);
        }

        private async Task PushMessagetoSQS(string messageBody, ILambdaContext context)
        {
            var sqsClient = new AmazonSQSClient();
            context.Logger.LogLine($"Message {messageBody} added to queue\n  {this.SQSUrl}");
            SendMessageResponse responseSendMsg =
              await sqsClient.SendMessageAsync(this.SQSUrl, messageBody);
            context.Logger.LogLine($"HttpStatusCode: {responseSendMsg.HttpStatusCode}");
        }

        private string SQSUrl
        {
            get
            {
                return Environment.GetEnvironmentVariable("SQS_URL");
            }
        }

        private string ConnectionString
        {
            get
            {
                return Environment.GetEnvironmentVariable("DB_CONNECTION");
            }
        }
    }
}
