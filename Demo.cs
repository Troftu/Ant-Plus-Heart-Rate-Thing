/*
This software is subject to the license described in the License.txt file
included with this software distribution. You may not use this file except
in compliance with this license.

Copyright (c) Dynastream Innovations Inc. 2016
All rights reserved.
*/

//////////////////////////////////////////////////////////////////////////
// To use the managed library, you must:
// 1. Import ANT_NET.dll as a reference
// 2. Reference the ANT_Managed_Library namespace
// 3. Include the following files in the working directory of your application:
//  - DSI_CP310xManufacturing_3_1.dll
//  - DSI_SiUSBXp_3_1.dll
//  - ANT_WrappedLib.dll
//  - ANT_NET.dll
//////////////////////////////////////////////////////////////////////////

#define ENABLE_EXTENDED_MESSAGES // Un - coment to enable extended messages

using System.Text;
using ANT_Managed_Library;

namespace ANT_Console_Demo
{
    public class Demo
    {
        private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        "heart_rate.txt");
        
        private static readonly byte CHANNEL_TYPE_INVALID = 2;

        private static readonly byte USER_ANT_CHANNEL = 0;         // ANT Channel to use
        private static readonly ushort USER_DEVICENUM = 0;        // Device number    
        private static readonly byte USER_DEVICETYPE = 120;          // Device type
        private static readonly byte USER_TRANSTYPE = 0;           // Transmission type

        private static readonly byte USER_RADIOFREQ = 57;          // RF Frequency + 2400 MHz
        private static readonly ushort USER_CHANNELPERIOD = 8070;  // Channel Period (8192/32768)s period = 4Hz

        private static readonly byte[] USER_NETWORK_KEY = NetworKey.Key;
        private static readonly byte USER_NETWORK_NUM = 0;         // The network key is assigned to this network number

        private static ANT_Device? device0;
        private static ANT_Channel? channel0;
        private static ANT_ReferenceLibrary.ChannelType channelType;
        private static byte[] txBuffer = [0, 0, 0, 0, 0, 0, 0, 0];
        private static bool bDone;
        private static bool bDisplay;
        private static bool bBroadcasting;
        private static int iIndex = 0;
        
        public static void Run()
        {
            try
            {
                Init();
                Start(CHANNEL_TYPE_INVALID);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ANT+ connection failed with error: \n" + ex.Message);
                throw;
            }
        }
        
        static void Init()
        {
            try
            {
                Console.WriteLine("Attempting to connect to an ANT USB device...");
                device0 = new ANT_Device();   // Create a device instance using the automatic constructor (automatic detection of USB device number and baud rate)
                //device0.deviceResponse += new ANT_Device.dDeviceResponseHandler(DeviceResponse);    // Add device response function to receive protocol event messages

                channel0 = device0.getChannel(USER_ANT_CHANNEL);    // Get channel from ANT device
                channel0.channelResponse += new dChannelResponseHandler(ChannelResponse);  // Add channel response function to receive channel event messages
                Console.WriteLine("Initialization was successful!");
            }
            catch (Exception ex)
            {
                if (device0 is null)    // Unable to connect to ANT
                {
                    throw new Exception("Could not connect to any device.\n" + 
                    "Details: \n   " + ex.Message);
                }
                else
                {
                    throw new Exception("Error connecting to ANT: " + ex.Message);
                }
            }
        }
        
        static void Start(byte ucChannelType_)
        {
            byte ucChannelType = 1;
            bDone = false;
            bDisplay = true;
            bBroadcasting = false;
            
            // If a channel type has not been set at the command line,
            // prompt the user to specify one now
            do
            {
                if (ucChannelType == CHANNEL_TYPE_INVALID)
                {
                    Console.WriteLine("Channel Type? (Master = 0, Slave = 1)");
                    try
                    {
                        ucChannelType = byte.Parse(Console.ReadLine());
                    }
                    catch (Exception)
                    {
                        ucChannelType = CHANNEL_TYPE_INVALID;
                    }
                }

                if (ucChannelType == 0)
                {
                    channelType = ANT_ReferenceLibrary.ChannelType.BASE_Master_Transmit_0x10;
                }
                else if (ucChannelType == 1)
                {
                    channelType = ANT_ReferenceLibrary.ChannelType.BASE_Slave_Receive_0x00;
                }
                else
                {
                    ucChannelType = CHANNEL_TYPE_INVALID;
                    Console.WriteLine("Error: Invalid channel type");
                }
            } while (ucChannelType == CHANNEL_TYPE_INVALID);

            try
            {
                ConfigureANT();

                while (!bDone)
                {
                    System.Threading.Thread.Sleep(100);
                }

                // Clean up ANT
                Console.WriteLine("Disconnecting module...");
                ANT_Device.shutdownDeviceInstance(ref device0);
            }
            catch (Exception ex)
            {
                throw new Exception("Demo failed: " + ex.Message + Environment.NewLine);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////
        // ConfigureANT
        //
        // Resets the system, configures the ANT channel and starts the demo
        ////////////////////////////////////////////////////////////////////////////////
        private static void ConfigureANT()
        {
            Console.WriteLine("Resetting module...");
            device0.ResetSystem();     // Soft reset
            System.Threading.Thread.Sleep(500);    // Delay 500ms after a reset

            // If you call the setup functions specifying a wait time, you can check the return value for success or failure of the command
            // This function is blocking - the thread will be blocked while waiting for a response.
            // 500ms is usually a safe value to ensure you wait long enough for any response
            // If you do not specify a wait time, the command is simply sent, and you have to monitor the protocol events for the response,
            Console.WriteLine("Setting network key...");
            if (device0.setNetworkKey(USER_NETWORK_NUM, USER_NETWORK_KEY, 500))
                Console.WriteLine("Network key set");
            else
                throw new Exception("Error configuring network key");

            Console.WriteLine("Assigning channel...");
            if (channel0.assignChannel(channelType, USER_NETWORK_NUM, 500))
                Console.WriteLine("Channel assigned");
            else
                throw new Exception("Error assigning channel");

            Console.WriteLine("Setting Channel ID...");
            if (channel0.setChannelID(USER_DEVICENUM, false, USER_DEVICETYPE, USER_TRANSTYPE, 500))  // Not using pairing bit
                Console.WriteLine("Channel ID set");
            else
                throw new Exception("Error configuring Channel ID");

            Console.WriteLine("Setting Radio Frequency...");
            if (channel0.setChannelFreq(USER_RADIOFREQ, 500))
                Console.WriteLine("Radio Frequency set");
            else
                throw new Exception("Error configuring Radio Frequency");

            Console.WriteLine("Setting Channel Period...");
            if (channel0.setChannelPeriod(USER_CHANNELPERIOD, 500))
                Console.WriteLine("Channel Period set");
            else 
                throw new Exception("Error configuring Channel Period");

            Console.WriteLine("Opening channel...");
            bBroadcasting = true;
            if (channel0.openChannel(500))
            {
                Console.WriteLine("Channel opened");
            }
            else
            {
                bBroadcasting = false;
                throw new Exception("Error opening channel");
            }

#if (ENABLE_EXTENDED_MESSAGES)
            // Extended messages are not supported in all ANT devices, so
            // we will not wait for the response here, and instead will monitor 
            // the protocol events
            Console.WriteLine("Enabling extended messages...");
            device0.enableRxExtendedMessages(true);
#endif
        }

      
        private static void ChannelResponse(ANT_Response response)
        {
            try
            {
                var messageId = (ANT_ReferenceLibrary.ANTMessageID)response.responseID;
                switch (messageId)
                {
                    case ANT_ReferenceLibrary.ANTMessageID.BROADCAST_DATA_0x4E:
                        try
                        {
                            var heartRate = response.getDataPayload()[7];
                            Console.WriteLine(heartRate);
                            File.WriteAllText(FilePath, $"{heartRate}", Encoding.UTF8);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }

                        break;
                    case ANT_ReferenceLibrary.ANTMessageID.RESPONSE_EVENT_0x40:
                    case ANT_ReferenceLibrary.ANTMessageID.INVALID_0x00:
                    case ANT_ReferenceLibrary.ANTMessageID.EVENT_0x01:
                    case ANT_ReferenceLibrary.ANTMessageID.VERSION_0x3E:
                    case ANT_ReferenceLibrary.ANTMessageID.UNASSIGN_CHANNEL_0x41:
                    case ANT_ReferenceLibrary.ANTMessageID.ASSIGN_CHANNEL_0x42:
                    case ANT_ReferenceLibrary.ANTMessageID.CHANNEL_MESG_PERIOD_0x43:
                    case ANT_ReferenceLibrary.ANTMessageID.CHANNEL_SEARCH_TIMEOUT_0x44:
                    case ANT_ReferenceLibrary.ANTMessageID.CHANNEL_RADIO_FREQ_0x45:
                    case ANT_ReferenceLibrary.ANTMessageID.NETWORK_KEY_0x46:
                    case ANT_ReferenceLibrary.ANTMessageID.RADIO_TX_POWER_0x47:
                    case ANT_ReferenceLibrary.ANTMessageID.RADIO_CW_MODE_0x48:
                    case ANT_ReferenceLibrary.ANTMessageID.SYSTEM_RESET_0x4A:
                    case ANT_ReferenceLibrary.ANTMessageID.OPEN_CHANNEL_0x4B:
                    case ANT_ReferenceLibrary.ANTMessageID.CLOSE_CHANNEL_0x4C:
                    case ANT_ReferenceLibrary.ANTMessageID.REQUEST_0x4D:
                    case ANT_ReferenceLibrary.ANTMessageID.ACKNOWLEDGED_DATA_0x4F:
                    case ANT_ReferenceLibrary.ANTMessageID.BURST_DATA_0x50:
                    case ANT_ReferenceLibrary.ANTMessageID.CHANNEL_ID_0x51:
                    case ANT_ReferenceLibrary.ANTMessageID.CHANNEL_STATUS_0x52:
                    case ANT_ReferenceLibrary.ANTMessageID.RADIO_CW_INIT_0x53:
                    case ANT_ReferenceLibrary.ANTMessageID.CAPABILITIES_0x54:
                    case ANT_ReferenceLibrary.ANTMessageID.STACKLIMIT_0x55:
                    case ANT_ReferenceLibrary.ANTMessageID.SCRIPT_DATA_0x56:
                    case ANT_ReferenceLibrary.ANTMessageID.SCRIPT_CMD_0x57:
                    case ANT_ReferenceLibrary.ANTMessageID.ID_LIST_ADD_0x59:
                    case ANT_ReferenceLibrary.ANTMessageID.ID_LIST_CONFIG_0x5A:
                    case ANT_ReferenceLibrary.ANTMessageID.OPEN_RX_SCAN_0x5B:
                    case ANT_ReferenceLibrary.ANTMessageID.EXT_BROADCAST_DATA_0x5D:
                    case ANT_ReferenceLibrary.ANTMessageID.EXT_ACKNOWLEDGED_DATA_0x5E:
                    case ANT_ReferenceLibrary.ANTMessageID.EXT_BURST_DATA_0x5F:
                    case ANT_ReferenceLibrary.ANTMessageID.CHANNEL_RADIO_TX_POWER_0x60:
                    case ANT_ReferenceLibrary.ANTMessageID.GET_SERIAL_NUM_0x61:
                    case ANT_ReferenceLibrary.ANTMessageID.GET_TEMP_CAL_0x62:
                    case ANT_ReferenceLibrary.ANTMessageID.SET_LP_SEARCH_TIMEOUT_0x63:
                    case ANT_ReferenceLibrary.ANTMessageID.SERIAL_NUM_SET_CHANNEL_ID_0x65:
                    case ANT_ReferenceLibrary.ANTMessageID.RX_EXT_MESGS_ENABLE_0x66:
                    case ANT_ReferenceLibrary.ANTMessageID.ENABLE_LED_FLASH_0x68:
                    case ANT_ReferenceLibrary.ANTMessageID.XTAL_ENABLE_0x6D:
                    case ANT_ReferenceLibrary.ANTMessageID.STARTUP_MESG_0x6F:
                    case ANT_ReferenceLibrary.ANTMessageID.AUTO_FREQ_CONFIG_0x70:
                    case ANT_ReferenceLibrary.ANTMessageID.PROX_SEARCH_CONFIG_0x71:
                    case ANT_ReferenceLibrary.ANTMessageID.ADV_BURST_DATA_0x72:
                    case ANT_ReferenceLibrary.ANTMessageID.EVENT_BUFFER_CONFIG_0x74:
                    case ANT_ReferenceLibrary.ANTMessageID.SET_SEARCH_PRIORITY_LEVEL_0x75:
                    case ANT_ReferenceLibrary.ANTMessageID.HIGH_DUTY_SEARCH_CONFIG_0x77:
                    case ANT_ReferenceLibrary.ANTMessageID.ADV_BURST_CONFIG_0x78:
                    case ANT_ReferenceLibrary.ANTMessageID.EVENT_FILTER_CONFIG_0x79:
                    case ANT_ReferenceLibrary.ANTMessageID.SDU_CONFIG_0x7A:
                    case ANT_ReferenceLibrary.ANTMessageID.SET_SDU_MASK_0x7B:
                    case ANT_ReferenceLibrary.ANTMessageID.USER_NVM_CONFIG_0x7C:
                    case ANT_ReferenceLibrary.ANTMessageID.ENABLE_ENCRYPTION_0x7D:
                    case ANT_ReferenceLibrary.ANTMessageID.SET_ENCRYPTION_KEY_0x7E:
                    case ANT_ReferenceLibrary.ANTMessageID.SET_ENCRYPTION_INFO_0x7F:
                    case ANT_ReferenceLibrary.ANTMessageID.SET_SEARCH_SHARING_CYCLES_0x81:
                    case ANT_ReferenceLibrary.ANTMessageID.ENCRYPTION_KEY_NVM_OPERATION_0x83:
                    case ANT_ReferenceLibrary.ANTMessageID.FIT1_SET_AGC_0x8F:
                    case ANT_ReferenceLibrary.ANTMessageID.FIT1_SET_EQUIP_STATE_0x91:
                    case ANT_ReferenceLibrary.ANTMessageID.SET_CHANNEL_INPUT_MASK_0x90:
                    case ANT_ReferenceLibrary.ANTMessageID.READ_PINS_FOR_SECT_0x92:
                    case ANT_ReferenceLibrary.ANTMessageID.TIMER_SELECT_0x93:
                    case ANT_ReferenceLibrary.ANTMessageID.ATOD_SETTINGS_0x94:
                    case ANT_ReferenceLibrary.ANTMessageID.SET_SHARED_ADDRESS_0x95:
                    case ANT_ReferenceLibrary.ANTMessageID.RSSI_POWER_0xC0:
                    case ANT_ReferenceLibrary.ANTMessageID.RSSI_BROADCAST_DATA_0xC1:
                    case ANT_ReferenceLibrary.ANTMessageID.RSSI_ACKNOWLEDGED_DATA_0xC2:
                    case ANT_ReferenceLibrary.ANTMessageID.RSSI_BURST_DATA_0xC3:
                    case ANT_ReferenceLibrary.ANTMessageID.RSSI_SEARCH_THRESHOLD_0xC4:
                    case ANT_ReferenceLibrary.ANTMessageID.SLEEP_0xC5:
                    case ANT_ReferenceLibrary.ANTMessageID.SET_USB_INFO_0xC7:
                    default:
                        // We do not care for these as of now.
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }
        }


        ////////////////////////////////////////////////////////////////////////////////
        // DeviceResponse
        //
        // Called whenever a message is received from ANT unless that message is a 
        // channel event message. 
        // 
        // response: ANT message
        ////////////////////////////////////////////////////////////////////////////////
        static void DeviceResponse(ANT_Response response)
        {
            switch ((ANT_ReferenceLibrary.ANTMessageID) response.responseID)
            {
                case ANT_ReferenceLibrary.ANTMessageID.STARTUP_MESG_0x6F:
                {
                    Console.Write("RESET Complete, reason: ");

                    byte ucReason = response.messageContents[0];

                    if(ucReason == (byte) ANT_ReferenceLibrary.StartupMessage.RESET_POR_0x00)
                        Console.WriteLine("RESET_POR");
                    if(ucReason == (byte) ANT_ReferenceLibrary.StartupMessage.RESET_RST_0x01)
                        Console.WriteLine("RESET_RST");
                    if(ucReason == (byte) ANT_ReferenceLibrary.StartupMessage.RESET_WDT_0x02)
                        Console.WriteLine("RESET_WDT");
                    if(ucReason == (byte) ANT_ReferenceLibrary.StartupMessage.RESET_CMD_0x20)
                        Console.WriteLine("RESET_CMD");
                    if(ucReason == (byte) ANT_ReferenceLibrary.StartupMessage.RESET_SYNC_0x40)
                        Console.WriteLine("RESET_SYNC");
                    if(ucReason == (byte) ANT_ReferenceLibrary.StartupMessage.RESET_SUSPEND_0x80)
                        Console.WriteLine("RESET_SUSPEND");
                    break;
                }
                case ANT_ReferenceLibrary.ANTMessageID.VERSION_0x3E:
                {
                    Console.WriteLine("VERSION: " + new ASCIIEncoding().GetString(response.messageContents));
                    break;
                }
                case ANT_ReferenceLibrary.ANTMessageID.RESPONSE_EVENT_0x40:
                {
                    switch (response.getMessageID())
                    {
                        case ANT_ReferenceLibrary.ANTMessageID.CLOSE_CHANNEL_0x4C:
                        {
                            if (response.getChannelEventCode() == ANT_ReferenceLibrary.ANTEventID.CHANNEL_IN_WRONG_STATE_0x15)
                            {
                                Console.WriteLine("Channel is already closed");
                                Console.WriteLine("Unassigning Channel...");
                                if (channel0.unassignChannel(500))
                                {
                                    Console.WriteLine("Unassigned Channel");
                                    Console.WriteLine("Press enter to exit");
                                    bDone = true;
                                }
                            }
                            break;
                        }
                        case ANT_ReferenceLibrary.ANTMessageID.NETWORK_KEY_0x46:
                        case ANT_ReferenceLibrary.ANTMessageID.ASSIGN_CHANNEL_0x42:
                        case ANT_ReferenceLibrary.ANTMessageID.CHANNEL_ID_0x51:
                        case ANT_ReferenceLibrary.ANTMessageID.CHANNEL_RADIO_FREQ_0x45:
                        case ANT_ReferenceLibrary.ANTMessageID.CHANNEL_MESG_PERIOD_0x43:
                        case ANT_ReferenceLibrary.ANTMessageID.OPEN_CHANNEL_0x4B:
                        case ANT_ReferenceLibrary.ANTMessageID.UNASSIGN_CHANNEL_0x41:
                        {
                            if (response.getChannelEventCode() != ANT_ReferenceLibrary.ANTEventID.RESPONSE_NO_ERROR_0x00)
                            {
                                Console.WriteLine(String.Format("Error {0} configuring {1}", response.getChannelEventCode(), response.getMessageID()));
                            }
                            break;
                        }
                        case ANT_ReferenceLibrary.ANTMessageID.RX_EXT_MESGS_ENABLE_0x66:
                        {
                            if (response.getChannelEventCode() == ANT_ReferenceLibrary.ANTEventID.INVALID_MESSAGE_0x28)
                            {
                                Console.WriteLine("Extended messages not supported in this ANT product");
                                break;
                            }
                            else if(response.getChannelEventCode() != ANT_ReferenceLibrary.ANTEventID.RESPONSE_NO_ERROR_0x00)
                            {
                                Console.WriteLine(String.Format("Error {0} configuring {1}", response.getChannelEventCode(), response.getMessageID()));
                                break;
                            }
                            Console.WriteLine("Extended messages enabled");
                            break;
                        }
                        case ANT_ReferenceLibrary.ANTMessageID.REQUEST_0x4D:
                        {
                            if (response.getChannelEventCode() == ANT_ReferenceLibrary.ANTEventID.INVALID_MESSAGE_0x28)
                            {
                                Console.WriteLine("Requested message not supported in this ANT product");
                                break;
                            }
                            break;
                        }
                        default:
                        {
                            Console.WriteLine("Unhandled response " + response.getChannelEventCode() + " to message " + response.getMessageID());                            break;
                        }
                    }
                    break;
                }
            }
        }


        ////////////////////////////////////////////////////////////////////////////////
        // PrintMenu
        //
        // Display demo menu
        // 
        ////////////////////////////////////////////////////////////////////////////////
        static void PrintMenu()
        {
            // Print out options  
            Console.WriteLine("M - Print this menu");
            Console.WriteLine("A - Send Acknowledged message");
            Console.WriteLine("B - Send Burst message");
            Console.WriteLine("R - Reset");
            Console.WriteLine("C - Request Capabilites");
            Console.WriteLine("V - Request Version");
            Console.WriteLine("I - Request Channel ID");
            Console.WriteLine("S - Request Status");
	        Console.WriteLine("U - Request USB Descriptor");
            Console.WriteLine("D - Toggle Display");
            Console.WriteLine("Q - Quit");
        }

    }
}
