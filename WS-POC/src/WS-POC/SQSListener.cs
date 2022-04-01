using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Amazon;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Amazon.SQS.Model;
using Newtonsoft.Json;

namespace WS_POC
{
    public class SQSListener
    {
        public IAmazonS3 S3Client;

        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
        /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
        /// region the Lambda function is executed in.
        /// </summary>
        public SQSListener()
        {

        }


        /// <summary>
        /// This method is called for every Lambda invocation. This method takes in an SNS event object and can be used 
        /// to respond to SNS messages.
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<bool> SQSListenerHandler(SQSEvent evnt, ILambdaContext context)
        {
            if (evnt.Records.Count > 0)
            {
                var message = await ProcessMessageAsync(evnt.Records[0], context);
                if (await SendEmail(message, context))
                {
                    await Updatenotifiedfieldforfaci(Convert.ToInt32(message.attendee_pk), context);
                    return true;
                }
                return false;
            }
            return false;
        }

        private async Task<Models.Message> ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
        {
            context.Logger.LogLine($"Processed message { message.Body}");
            try
            {
                var recepient = JsonConvert.DeserializeObject<Models.RecepientDetails>(message.Body);
                context.Logger.LogLine($"Receipint Object found {recepient.name}, {recepient.attendee_pk}");
                return await CreateMailBody(recepient, context);
            }
            catch (Exception ex)
            {
                context.Logger.LogLine($"There is error in casting object {ex.Message}");
            }

            // TODO: Do interesting work based on the new message
            return null;
        }

        private async Task<Models.Message> CreateMailBody(Models.RecepientDetails recepient, ILambdaContext context)
        {
            XmlDocument DocElement = await GetMailTemplatesFromS3(context);
            if (DocElement != null && recepient != null)
            {
                Models.Message message = new Models.Message();
                message.Subject = "New enrollee information";
                message.IsBodyHtml = true;

                message.Body = string.Format(DocElement.DocumentElement.ChildNodes[7].InnerText, recepient.title, recepient.schedule, recepient.location,
                    recepient.name, recepient.email, recepient.Address1, recepient.Address2, recepient.home_phone, recepient.work_phone, recepient.Room);

                string[] sessiondelete_recipient = recepient.emails.Split(';');
                StringBuilder ss = new StringBuilder();
                //foreach (string eachrecipient in sessiondelete_recipient)
                //{
                //    //checking for format of email
                //    if (new System.Text.RegularExpressions.Regex(@"\w+([-+.]\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*").IsMatch(eachrecipient))
                //    {
                //        message.To.Add(new MailAddress(eachrecipient));
                //    }
                //}
                message.FromAddress = "hardik.panchal@bacancy.com";
                message.ToAddress = "hardik.panchal@esc4.net";

                return message;
            }
            return null;
        }

        private async Task<XmlDocument> GetMailTemplatesFromS3(ILambdaContext context)
        {
            XmlDocument DocElement = new XmlDocument();
            var bucketName = S3BucketName;//"esc-04-mail-template";//;
            var fileName = S3FileName;// "tx_esc_04NotificationServices.xml";//;
            context.Logger.LogLine($"Bucket Name : {bucketName}");
            context.Logger.LogLine($"File Name : {fileName}");
            try
            {
                GetObjectResponse response = new GetObjectResponse();
                var request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = fileName
                };
                S3Client = new AmazonS3Client(RegionEndpoint.USEast1);
                response = await S3Client.GetObjectAsync(request);

                DocElement.Load(response.ResponseStream);
                return DocElement;

            }
            catch (Exception ex)
            {
                context.Logger.LogLine($"There is error in fetching S3 object {ex.Message}");
            }
            return null;
        }

        private async Task<bool> SendEmail(Models.Message message, ILambdaContext context)
        {
            try
            {
                using (var client = new AmazonSimpleEmailServiceClient(RegionEndpoint.USEast1))
                {
                    var sendRequest = new SendEmailRequest
                    {
                        Source = message.FromAddress,
                        Destination = new Destination
                        {
                            ToAddresses =
                            new List<string> { message.ToAddress }
                        },
                        Message = new Amazon.SimpleEmail.Model.Message
                        {
                            Subject = new Content(message.Subject),
                            Body = new Body
                            {
                                Html = new Content
                                {
                                    Data = message.Body
                                },
                                Text = new Content
                                {
                                    Charset = "UTF-8",
                                    Data = message.Body
                                }
                            }
                        },
                        // If you are not using a configuration set, comment
                        // or remove the following line 
                        //ConfigurationSetName = configSet
                    };
                    try
                    {
                        context.Logger.LogLine("Sending email using Amazon SES...");
                        var response = await client.SendEmailAsync(sendRequest);
                        context.Logger.LogLine("The email was sent successfully.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        context.Logger.LogLine("The email was not sent.");
                        context.Logger.LogLine("Error message: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                context.Logger.LogLine($"There is error in sending SES {ex.Message}");
            }
            return false;
        }

        private async Task Updatenotifiedfieldforfaci(int obj_id, ILambdaContext context)
        {
            using (SqlCommand command = new SqlCommand("[/sysmail/Notification/notifyToFaci/update]", new SqlConnection(this.ConnectionString)))
            {
                command.Connection.Open();
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add("@obj_id", SqlDbType.Int, 4).Value = obj_id;
                try
                {
                    command.ExecuteNonQuery();
                    context.Logger.LogLine($"Database Query Update for the attendance {obj_id}");
                }
                catch (Exception ex)
                {
                    context.Logger.LogLine($"Error in executing command {ex.Message}");
                }
                finally
                {

                    if (command.Connection.State != ConnectionState.Closed)
                        command.Connection.Close();

                }
                await Task.CompletedTask;
            }
        }

        private string S3BucketName
        {
            get
            {
                return System.Environment.GetEnvironmentVariable("S3_Bucket_Name") ?? "esc-04-mail-template";
            }
        }

        private string S3FileName
        {
            get
            {
                return System.Environment.GetEnvironmentVariable("S3_File_Name") ?? "tx_esc_04NotificationServices.xml";
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
