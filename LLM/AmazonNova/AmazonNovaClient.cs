using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;

using Interfaces;

using LLMApiModels;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AmazonNova
{
    public class AmazonNovaClient : ILLMClient
    {
        private readonly AmazonBedrockRuntimeClient _amazonBedrockRuntimeClient;
        private const string ModelId = "us.amazon.nova-pro-v1:0";

        private const string ContentTypeImagePng = "image/png";
        private const string ContentTypeImageJpeg = "image/jpeg";
        private const string ContentTypeImageJpg = "image/jpg";
        private const string ContentTypeImageGif = "image/gif";
        private const string ContentTypePdf = "application/pdf";

        public AmazonNovaClient(IAWSCredentials awsCredentials)
        {
            _amazonBedrockRuntimeClient = awsCredentials.AwsS3RegionLogin
                ? new AmazonBedrockRuntimeClient(RegionEndpoint.GetBySystemName(awsCredentials.AwsRegion))
                : new AmazonBedrockRuntimeClient(
                    awsAccessKeyId: awsCredentials.AwsAccessKey,
                    awsSecretAccessKey: awsCredentials.AwsSecretAccessKey,
                    region: RegionEndpoint.GetBySystemName(systemName: awsCredentials.AwsRegion));
        }

        public bool WorkingWithImages => true;

        public bool WorkingWithDocuments => true;

        private bool IsImage(string contentType)
        {
            return contentType.Equals(ContentTypeImagePng, StringComparison.OrdinalIgnoreCase);
        }

        private DocumentFormat GetDocumentFormat(string contentType)
        {
            switch (contentType)
            {
                case ContentTypePdf:
                    return DocumentFormat.Pdf;
                default:
                    return DocumentFormat.Pdf;
            }
        }

        private ImageFormat GetImageFormat(string contentType)
        {
            switch (contentType)
            {
                case ContentTypeImagePng:
                    return ImageFormat.Png;
                case ContentTypeImageJpeg:
                    return ImageFormat.Jpeg;
                case ContentTypeImageJpg:
                    return ImageFormat.Jpeg;
                case ContentTypeImageGif:
                    return ImageFormat.Gif;
                default:
                    return ImageFormat.Png;
            }
        }

        private ContentBlock CreateImageContentBlock(FileData fileData)
        {
            return new ContentBlock
            {
                Image = new ImageBlock
                {
                    Format = GetImageFormat(fileData.ContentType),
                    Source = new ImageSource
                    {
                        Bytes = new MemoryStream(fileData.Data)
                    }
                }
            };
        }

        private ContentBlock CreateDocumentContentBlock(FileData fileData)
        {
            return new ContentBlock
            {
                Document = new DocumentBlock
                {
                    Format = GetDocumentFormat(fileData.ContentType),
                    Name = "Document", // Consider using a constant if needed.
                    Source = new DocumentSource
                    {
                        Bytes = new MemoryStream(fileData.Data)
                    }
                }
            };
        }

        private ContentBlock CreateTextContentBlock(string text)
        {
            return new ContentBlock
            {
                Text = text
            };
        }

        public ConverseRequest CreateConversionRequest(LLMApiRequest request)
        {
            var converseRequest = new ConverseRequest()
            {
                ModelId = ModelId
            };

            List<LLMApiRequestMessage> systemMessages = request.Messages
                .Where(x => x.Role == RequestMessageRoles.System)
                .ToList();

            if (systemMessages.Any())
            {
                converseRequest.System = systemMessages
                    .Select(x => new SystemContentBlock() { Text = x.TextContent })
                    .ToList();
            }

            IEnumerable<LLMApiRequestMessage> nonSystemMessages = request.Messages
                .Where(x => x.Role != RequestMessageRoles.System);

            foreach (LLMApiRequestMessage message in nonSystemMessages)
            {
                List<ContentBlock> contentBlocks = new List<ContentBlock>();
                if (message.Content != null)
                {
                    if (message.Content.Images != null)
                    {
                        foreach (FileData messageContent in message.Content.Images)
                        {
                            if (IsImage(messageContent.ContentType))
                            {
                                contentBlocks.Add(CreateImageContentBlock(messageContent));
                            }
                            else
                            {
                                contentBlocks.Add(CreateDocumentContentBlock(messageContent));
                            }
                        }
                    }

                    if (message.Content.Texts != null)
                    {
                        foreach (string messageContent in message.Content.Texts)
                        {
                            contentBlocks.Add(CreateTextContentBlock(messageContent));
                        }
                    }
                }

                if (!string.IsNullOrEmpty(message.TextContent))
                {
                    contentBlocks.Add(CreateTextContentBlock(message.TextContent));
                }

                converseRequest.Messages.Add(new Message
                {
                    Role = new ConversationRole(message.Role.ToString().ToLower()),
                    Content = contentBlocks
                });
            }

            return converseRequest;
        }

        public async Task<LLMApiResponse> CallAsync(LLMApiRequest request)
        {
            LLMApiResponse resp = await ConverseAsync(request);

            return resp;
        }

        public async Task<LLMApiResponse> ConverseAsync(LLMApiRequest request)
        {
            ConverseRequest conversionRequest = CreateConversionRequest(request);
            LLMApiResponse converseResponse = new LLMApiResponse();

            try
            {
                ConverseResponse conversionResponse = await _amazonBedrockRuntimeClient.ConverseAsync(conversionRequest);
                List<ContentBlock>? responseContents = conversionResponse?.Output?.Message?.Content;

                if (responseContents != null)
                {
                    foreach (ContentBlock content in responseContents)
                    {
                        if (!string.IsNullOrEmpty(content.Text))
                        {
                            converseResponse.Messages.Add(content.Text);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                converseResponse.Messages.Add(ex.Message);
            }

            return converseResponse;
        }
    }
}