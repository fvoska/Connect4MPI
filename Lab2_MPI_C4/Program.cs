using System;
using System.Collections.Generic;
using MPI;
using C4_Game;
using System.Diagnostics;
using System.Threading;

namespace Lab2_MPI_C4
{
    [Serializable]
    class Task
    {
        public StateNode previousState;
        public GameState currentState;
    }
    [Serializable]
    class TaskResult
    {
        public StateNode previousState;
        public double result;
    }

    class Program
    {
        static C4Game game = new C4Game();
        static int targetDepth = 6;
        static int searchDepth = 0;
        static int initialDepth = 0;
        static int minTasks = 0;
        static int gameWidth = 0;
        static Stopwatch sw = new Stopwatch();

        static void Main(string[] args)
        {
            using (new MPI.Environment(ref args))
            {
                Intracommunicator comm = Communicator.world;

                if (comm.Rank == 0)
                {
                    Console.WriteLine("Number of processors: " + comm.Size);

                    gameWidth = game.CurrentRoot.BoardState.Width;

                    minTasks = comm.Size * comm.Size;
                    Console.WriteLine("Recommended number of tasks: " + minTasks);

                    initialDepth = Convert.ToInt32(Math.Ceiling(Math.Log(minTasks) / Math.Log(gameWidth)));
                    searchDepth = targetDepth - initialDepth;
                    Console.WriteLine("Initial depth: " + initialDepth);
                    Console.WriteLine("Subtree depth: " + searchDepth);
                    Console.WriteLine("Expected number of tasks: " + Math.Pow(gameWidth, initialDepth));
                    Console.WriteLine();

                    game.PrintCurrentState();
                }

                tick(comm);
            }
        }

        static void tick(Intracommunicator comm)
        {
            byte winnerId = 0;
            
            // Generate and distribute tasks.
            int myTasksNum = 0;
            List<Task> myTasks = new List<Task>();
            if (comm.Rank == 0)
            {
                // Get input from console.
                Console.WriteLine("\nYour Turn:");
                int insertColumn;
                if (int.TryParse(Console.ReadLine(), out insertColumn))
                {
                    // Insert player's token.
                    bool inserted = game.Insert(insertColumn);
                    if (!inserted)
                    {
                        Console.WriteLine("Invalid column number. Please use numbers 0-" + (gameWidth - 1).ToString() + ".");
                        tick(comm);
                        return;
                    }

                    // Print state.
                    game.PrintCurrentState();

                    // Check if someone won. (Actually, only player can win at this point)
                    winnerId = game.CheckState();
                    if (winnerId != 0)
                    {
                        Console.WriteLine("\nYou won!\n");
                        // Start new game.
                        game = new C4Game();
                        Console.WriteLine("New Game\n");
                        game.PrintCurrentState();
                        tick(comm);
                        return;
                    }

                    // CPU's turn.
                    Console.WriteLine("\nCPU Turn:");
                    sw.Start();

                    // Generate tree.
                    game.GenerateTree(initialDepth);
                    List<Task> tasks = new List<Task>();
                    fillTasks(tasks, game.CurrentRoot);
                    Console.WriteLine("Number of tasks: " + tasks.Count);

                    Dictionary<int, int> numTasks = new Dictionary<int, int>();

                    // Count number of tasks that each process will get.
                    for (var t = 0; t < tasks.Count; t++)
                    {
                        int recv = t % comm.Size;
                        if (!numTasks.ContainsKey(recv))
                        {
                            numTasks[recv] = 0;
                        }
                        numTasks[recv]++;
                    }

                    // Tell each process how many tasks they will get.
                    for (var t = 0; t < comm.Size; t++)
                    {
                        int recv = t;
                        if (recv == 0)
                        {
                            myTasksNum = numTasks[recv];
                        }
                        else
                        {
                            comm.Send(numTasks[recv], recv, 0);
                        }
                        /*
                         * tag 0 -> number of tasks
                         */
                    }

                    // Send tasks.
                    for (var t = 0; t < tasks.Count; t++)
                    {
                        int recv = t % comm.Size;
                        if (recv == 0)
                        {
                            myTasks.Add(tasks[t]);
                        }
                        else
                        {
                            comm.Send(tasks[t], recv, 1);
                        }
                        /*
                         * tag 1 -> actual task
                         */
                    }
                }
                else
                {
                    Console.WriteLine("Invalid column number. Please use numbers 0-" + (gameWidth - 1).ToString() + ".");
                    tick(comm);
                    return;
                }
            }

            Console.WriteLine("Process #" + comm.Rank + " is waiting.");
            comm.Barrier();
            Console.WriteLine("Process #" + comm.Rank + " is continuing.");
            comm.Barrier();

            // Receive tasks.
            if (comm.Rank != 0)
            {
                // Get how many tasks I need.
                myTasksNum = comm.Receive<int>(0, 0);
                for (var n = 0; n < myTasksNum; n++)
                {
                    myTasks.Add(comm.Receive<Task>(0, 1));
                }
                
            }
            Console.WriteLine("Process #" + comm.Rank + " has " + myTasks.Count + " tasks.");

            // Process tasks.
            Dictionary<StateNode, double> results = new Dictionary<StateNode, double>();
            List<TaskResult> myTaskResults = new List<TaskResult>();
            foreach(Task task in myTasks)
            {
                C4Game myGame = new C4Game();
                myGame.CurrentRoot.BoardState = task.currentState;
                myGame.CurrentRoot.PreviousBoardState = task.previousState;
                byte player = 1;
                if (initialDepth % 2 == 0)
                    player = 2;
                myGame.CurrentRoot.WhoseTurn = player;
                myGame.CurrentRoot.BoardState.LastPlayer = (byte)(3 - player);
                double score = myGame.DecideMove(false, searchDepth, player);
                if (comm.Rank == 0)
                {
                    if (task.previousState != null)
                        results[task.previousState] = score;
                }
                else
                {
                    TaskResult tr = new TaskResult();
                    tr.previousState = task.previousState;
                    tr.result = score;
                    myTaskResults.Add(tr);
                }
            }

            // Send task results back.
            if (comm.Rank != 0)
            {
                comm.Send<List<TaskResult>>(myTaskResults, 0, 2);
                /*
                 * tag 2 -> task results
                 */
            }
            else
            {
                for (int w = 1; w < comm.Size; w++)
                {
                    List<TaskResult> hisTaskResults = comm.Receive<List<TaskResult>>(w, 2);
                    foreach(TaskResult htr in hisTaskResults)
                    {
                        results[htr.previousState] = htr.result;
                    }
                }
            }

            comm.Barrier();

            if (comm.Rank == 0)
            {
                // Fill task values.
                fillResults(results, game.CurrentRoot);
                foreach (var m in game.CurrentRoot.PossibleMoves)
                {
                    m.BoardState.PrintBoard();
                    Console.WriteLine(m.Score);
                    Console.WriteLine();
                }
                // Decide move.
                game.DecideMove(true, initialDepth - 1, 2, true);
                sw.Stop();
                Console.WriteLine("Time: " + sw.Elapsed);
                sw.Reset();

                // Print state.
                game.PrintCurrentState();

                // Check if someone won. (Actually, only CPU can win at this point)
                winnerId = game.CheckState();
                if (winnerId != 0)
                {
                    Console.WriteLine("\nCPU won!\n");
                    // Start new game.
                    game = new C4Game();
                    Console.WriteLine("New Game\n");
                    game.PrintCurrentState();
                }
            }

            tick(comm);
            return;
        }

        static void fillTasks(List<Task> tasks, StateNode node)
        {
            if (node.PossibleMoves.Count == 0)
            {
                Task t = new Task();
                t.currentState = node.BoardState;
                t.previousState = node.PreviousBoardState;
                tasks.Add(t);
            }
            else
            {
                foreach (StateNode n in node.PossibleMoves)
                {
                    fillTasks(tasks, n);
                }
            }
        }

        static void fillResults(Dictionary<StateNode, double> results, StateNode node)
        {
            if (results.ContainsKey(node))
            {
                node.Score = results[node];
            }
            foreach(StateNode n in node.PossibleMoves)
            {
                fillResults(results, n);
            }
        }
    }
}
