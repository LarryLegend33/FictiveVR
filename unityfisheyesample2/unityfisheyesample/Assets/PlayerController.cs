using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System; 


public class PipeClientCreator
{
    public string tail_voltage, pipe_handle;
    public Tuple<float, float> tail_command; 

    public PipeClientCreator(string handle)
    {
        tail_voltage = "left_startval";
        pipe_handle = handle;
        tail_command = new Tuple<float, float>(0, 0);
    }

    public void VoltageParser()
    {
        string[] parsed_voltage = tail_voltage.Split(',');
        tail_command = new Tuple<float, float>(float.Parse(parsed_voltage[0]), float.Parse(parsed_voltage[1]));
    }

    public void StartClient()
        {
            using (PipeStream pipeClient =
                new AnonymousPipeClientStream(PipeDirection.In, pipe_handle))
            {
                using (StreamReader sr = new StreamReader(pipeClient))
                {
                // Read the server data and echo to the console.
                    while (true)
                    {
                       // tail_left = counter.ToString();
                        if (sr.Peek() >= 0)
                        {
                        //counter++;
                            tail_voltage = sr.ReadLine();
                            VoltageParser(); 
                        }
                    }
                }
            }
        }
    }



public class PlayerController : MonoBehaviour
{

    private float translational_gain = 15.0f;
    private float rotational_gain = 45.0f;
    private float horizontalInput, forwardInput;
    private string[] cmdLineArgs;
    public string pipe_input = "start";
    public PipeClientCreator pCC; 
    
   
    // Note that if you make this public, unity takes it on as a public variable.
    // It then cannot be set in here. 

    // Start is called before the first frame update

    void Start()
    {
        cmdLineArgs = System.Environment.GetCommandLineArgs(); 
        pCC = new PipeClientCreator(cmdLineArgs[1]);
        var pipeThread = new Thread(pCC.StartClient);
        pipeThread.Start(); 
    }

    void OnGUI()
    {
        try
        {
            GUI.Label(new Rect(10, 10, 100, 20), "T: " + translational_gain.ToString());
            GUI.Label(new Rect(10, 50, 100, 20), "R: " + rotational_gain.ToString());
        }

        catch 
        {

            GUI.Label(new Rect(10, 10, 100, 20), "Exception in OnGUI");
        }
     // GUI.Label(new Rect(40, 40, 100, 20), cmdLineArgs[0]);

    }

    // Update is called once per frame
    void Update()
    {

        if (Input.GetKey(KeyCode.UpArrow))
        {
            translational_gain += 1f;
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            translational_gain -= 1f;
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            rotational_gain += 1f;
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            rotational_gain -= 1f;
        }
       // translational_gain += Input.GetAxis("Vertical");
       // rotational_gain += Input.GetAxis("Horizontal");
        horizontalInput = (float)(Math.Abs(pCC.tail_command.Item2) - Math.Abs(pCC.tail_command.Item1)) * rotational_gain;
        //  horizontalInput = Input.GetAxis("Horizontal");
        //   forwardInput = Input.GetAxis("Vertical");
        forwardInput = (float)Math.Sqrt(Math.Pow(pCC.tail_command.Item2, 2) + Math.Pow(pCC.tail_command.Item1, 2)) * translational_gain; 
        // moves at 1 unit per second
        transform.Translate(Vector3.forward * Time.deltaTime * forwardInput);
        transform.Rotate(Vector3.up * Time.deltaTime * horizontalInput);
    }
}
