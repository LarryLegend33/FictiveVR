using System;
using System.IO;
using System.IO.Pipes;
using System.Diagnostics;

class PipeServer
{
    static void Main()
    {
        Process pipeClient = new Process();

        pipeClient.StartInfo.FileName = "C:/Users/martinH/Desktop/BuiltUnityGame/5cube2fish.exe";
        int counter = 0; 
        using (AnonymousPipeServerStream pipeServer =
            new AnonymousPipeServerStream(PipeDirection.Out,
            HandleInheritability.Inheritable))
        {
            Console.WriteLine("[SERVER] Current TransmissionMode: {0}.",
                pipeServer.TransmissionMode);

            // Pass the client process a handle to the server.
            pipeClient.StartInfo.Arguments =
                pipeServer.GetClientHandleAsString();
            pipeClient.StartInfo.UseShellExecute = false;
            pipeClient.Start();

            pipeServer.DisposeLocalCopyOfClientHandle();

            try
            {
                // Read user input and send that to the client process.
                using (StreamWriter sw = new StreamWriter(pipeServer))
                {
                    // sw.AutoFlush = true;
                    sw.AutoFlush = false;
                    Console.WriteLine("Before Loop");
                    while (counter < 200000)
                    {
                        //sw.AutoFlush = true;
                        // Send a 'sync message' and wait for client to receive it.
                        sw.WriteLine(counter.ToString());
                        Console.WriteLine("Writing Sample" + counter.ToString());
                        pipeServer.WaitForPipeDrain();
                        counter++;
                        // Send the console input to the client process.
                       // Console.Write("[SERVER] Enter text: ");
                       // sw.WriteLine(Console.ReadLine());
                    }
                    Console.WriteLine("Wrote 200K Samples");
                }
            }
            // Catch the IOException that is raised if the pipe is broken
            // or disconnected.
            catch (IOException e)
            {
                Console.WriteLine("[SERVER] Error: {0}", e.Message);
            }
        }

        pipeClient.WaitForExit();
        pipeClient.Close();
        Console.WriteLine("[SERVER] Client quit. Server terminating.");
    }
}