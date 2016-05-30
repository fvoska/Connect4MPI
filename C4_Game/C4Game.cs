using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

namespace C4_Game
{
    public class C4Game
    {
        // Current state from which game tree of possible moves is built.
        public StateNode CurrentRoot { get; private set; }

        /// <summary>
        /// Constructor for default game size.
        /// </summary>
        public C4Game()
        {
            CreateGame(7, 6);
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="width">Board width.</param>
        /// <param name="height">Board height.</param>
        public C4Game(int width, int height)
        {
            CreateGame(width, height);
        }

        /// <summary>
        /// Initialization.
        /// </summary>
        /// <param name="width">Board width.</param>
        /// <param name="height">Board height.</param>
        private void CreateGame(int width, int height)
        {
            GameState game = new GameState(width, height);

            StateNode gameTree = new StateNode(game);
            //HistoryRoot = (GameTree)gameTree.Clone();
            CurrentRoot = gameTree;
        }

        /// <summary>
        /// Checks state.
        /// </summary>
        /// <returns>1: CPU won; -1: Human won; 0: still no winner</returns>
        public byte CheckState()
        {
            return CurrentRoot.BoardState.CheckState();
        }

        /// <summary>
        /// Prints game tree.
        /// </summary>
        public void PrintTree()
        {
            if (CurrentRoot.PossibleMoves.Count == 0)
            {
                Console.WriteLine("Please generate possible moves first");
            }
            else
            {
                PrintTreeRec(CurrentRoot, CurrentRoot.TreeDepth(), CurrentRoot.TreeDepth());
            }

        }
        
        private void PrintTreeRec(StateNode node, int depth, int maxDepth)
        {
            if (maxDepth == depth)
            {
                Console.WriteLine("Current state:");
            }
            if (depth < 0)
                return;
            node.BoardState.PrintBoard(maxDepth - depth);
            if (maxDepth == depth)
            {
                Console.WriteLine("Possible moves are shown in shape of a tree.");
            }
            if (node.PossibleMoves.Count != 0)
            {
                PrintIndents(maxDepth - depth + 1);
                string playerMark = (node.WhoseTurn == 1) ? "X" : "O";
                Console.WriteLine("Player " + playerMark + "'s possible moves:");
            }
            foreach(StateNode move in node.PossibleMoves)
            {
                PrintTreeRec(move, depth - 1, maxDepth);
            }
            PrintIndents(maxDepth - depth);
            Console.WriteLine("State: " + node.State);
            PrintIndents(maxDepth - depth);
            Console.WriteLine("Score: " + node.Score);
            PrintIndents(maxDepth - depth);
            Console.WriteLine("Whose turn: " + node.WhoseTurn);
            PrintIndents(maxDepth - depth);
            Console.WriteLine("LastColumn: " + node.BoardState.LastColumn);
            Console.WriteLine();
        }

        /// <summary>
        /// Insert token.
        /// </summary>
        /// <param name="column">Column for insertion.</param>
        /// <param name="player">Player who inserts (1: Human; 2: CPU)</param>
        /// <returns></returns>
        public bool Insert(int column, byte player = 1)
        {
            bool success = CurrentRoot.BoardState.Insert(column, player);
            if (success)
                CurrentRoot = new StateNode(CurrentRoot.BoardState);
            return success;
        }

        /// <summary>
        /// Generate game tree of possible moves down to specified depth.
        /// </summary>
        /// <param name="depth">Game tree depth.</param>
        /// <param name="solveOnly">If set to true, algorithm will score already generated tree.</param>
        public void GenerateTree(int depth = 1, bool solveOnly = false)
        {
            GenerateTree(CurrentRoot, depth, depth, solveOnly);
        }

        private void GenerateTree(StateNode currentRoot, int depth, int maxDepth, bool solveOnly = false, Dictionary<StateNode, double> results = null)
        {
            // Determine win states.
            currentRoot.UpdateState();

            if (solveOnly)
            {
                // Begining from root (current state), generate all possible moves recursively.
                if (depth > 1 && currentRoot.State == 0)
                {
                    foreach (StateNode move in currentRoot.PossibleMoves)
                    {
                        // Call generation on each possible move recursively.
                        GenerateTree(move, depth - 1, maxDepth, solveOnly, results);
                    }
                }

                if (currentRoot.PreviousBoardState != null)
                {
                    // Now calculate states "backwards".
                    // First one to call this will be leaf, then his parent, and so on... until the root.
                    if (currentRoot.State == 1 && currentRoot.WhoseTurn == 1)
                    {
                        // CPU can win from upper state.
                        currentRoot.PreviousBoardState.State = 1;
                    }
                    else if (currentRoot.State == -1 && currentRoot.WhoseTurn == 2)
                    {
                        // Player can win from upper state.
                        currentRoot.PreviousBoardState.State = -1;
                    }
                }

                if (depth != 0)
                {
                    // Calculate score
                    int numMoves = currentRoot.PossibleMoves.Count;
                    int numWins = 0;
                    int numLoss = 0;
                    foreach (StateNode move in currentRoot.PossibleMoves)
                    {
                        if (move.State == 1)
                            numWins++;
                        else if (move.State == -1)
                            numLoss++;
                    }
                    if (numMoves == numWins)
                    {
                        // All possible moves lead to CPU victory.
                        currentRoot.State = 1;
                        currentRoot.Score = 1;
                    }
                    else if (numMoves == numLoss)
                    {
                        // All possible moves lead to Human victory.
                        currentRoot.State = -1;
                        currentRoot.Score = -1;
                    }
                    else
                    {
                        // it's not trivial (not all moves lead to win or loss)
                        currentRoot.WinCount += numWins;
                        currentRoot.LossCount += numLoss;
                        currentRoot.NoEndCount += currentRoot.PossibleMoves.Count - numWins - numLoss;
                        foreach (StateNode move in currentRoot.PossibleMoves)
                        {
                            currentRoot.WinCount += move.WinCount;
                            currentRoot.LossCount += move.LossCount;
                            currentRoot.NoEndCount += move.NoEndCount;
                        }
                        currentRoot.Score = (double)(currentRoot.WinCount - currentRoot.LossCount) / (currentRoot.WinCount + currentRoot.LossCount + currentRoot.NoEndCount);
                        //currentRoot.Score = currentRoot.PossibleMoves.Sum(m => m.Score) / currentRoot.PossibleMoves.Count;
                    }
                }
            }
            else
            {
                // Begining from root (current state), generate all possible moves recursively.
                if (depth != 0 && currentRoot.State == 0)
                {
                    // Generate possible moves for current node.
                    if (!solveOnly) currentRoot.GeneratePossibleMoves();

                    foreach (StateNode move in currentRoot.PossibleMoves)
                    {
                        // Call generation on each possible move recursively.
                        GenerateTree(move, depth - 1, maxDepth, solveOnly);
                    }
                }

                if (currentRoot.PreviousBoardState != null)
                {
                    // Now calculate states "backwards".
                    // First one to call this will be leaf, then his parent, and so on... until the root.
                    if (currentRoot.State == 1 && currentRoot.WhoseTurn == 1)
                    {
                        // CPU can win from upper state.
                        currentRoot.PreviousBoardState.State = 1;
                    }
                    else if (currentRoot.State == -1 && currentRoot.WhoseTurn == 2)
                    {
                        // Player can win from upper state.
                        currentRoot.PreviousBoardState.State = -1;
                    }
                }

                if (depth != 0)
                {
                    // Calculate score
                    int numMoves = currentRoot.PossibleMoves.Count;
                    int numWins = 0;
                    int numLoss = 0;
                    foreach (StateNode move in currentRoot.PossibleMoves)
                    {
                        if (move.State == 1)
                            numWins++;
                        else if (move.State == -1)
                            numLoss++;
                    }
                    if (numMoves == numWins)
                    {
                        // All possible moves lead to CPU victory.
                        currentRoot.State = 1;
                        currentRoot.Score = 1;
                    }
                    else if (numMoves == numLoss)
                    {
                        // All possible moves lead to Human victory.
                        currentRoot.State = -1;
                        currentRoot.Score = -1;
                    }
                    else
                    {
                        // it's not trivial (not all moves lead to win or loss)
                        currentRoot.WinCount += numWins;
                        currentRoot.LossCount += numLoss;
                        currentRoot.NoEndCount += currentRoot.PossibleMoves.Count - numWins - numLoss;
                        foreach (StateNode move in currentRoot.PossibleMoves)
                        {
                            currentRoot.WinCount += move.WinCount;
                            currentRoot.LossCount += move.LossCount;
                            currentRoot.NoEndCount += move.NoEndCount;
                        }
                        currentRoot.Score = (double)(currentRoot.WinCount - currentRoot.LossCount) / (currentRoot.WinCount + currentRoot.LossCount + currentRoot.NoEndCount);
                        //currentRoot.Score = currentRoot.PossibleMoves.Sum(m => m.Score) / currentRoot.PossibleMoves.Count;
                    }
                }
            }            
        }

        /// <summary>
        /// Decide move.
        /// </summary>
        /// <param name="depth">Game tree depth. Bigger number = "smarter" CPU.</param>
        /// <param name="player">1: player; 2: CPU (default)</param>
        public double DecideMove(bool insert = true, int depth = 4, byte player = 2, bool solveOnly = false)
        {
            GenerateTree(CurrentRoot, depth, depth, solveOnly);

            // Add some randomization for equally scored moves.
            CurrentRoot.PossibleMoves.Shuffle();

            // Order moves by score descending.
            List<StateNode> ordered;
            ordered = CurrentRoot.PossibleMoves.OrderByDescending(t => t.Score).ToList();

            if (ordered.Count == 0)
            {
                //PrintCurrentState();
                return CurrentRoot.Score;
            }

            // Start with highest scoring possible move.
            int i = 0;
            int bi = 0;
            StateNode bestMove = ordered[i];

            // Don't make move that leads human to victory.
            while (bestMove.State == -1)
            {
                i++;
                if (i >= ordered.Count)
                {
                    // Couldn't find a non-losing state. Play CPU's best option and hope human doesn't see his winning move.
                    bestMove = ordered[0];
                    bi = i;
                    break;
                }
                bestMove = ordered[i];
            }

            // Get column for move.
            int bestColumn = bestMove.BoardState.LastColumn;

            if (insert)
            {
                // Finally, make the move.
                Insert(bestColumn, player);

                // Inform human of the move.
                Console.WriteLine("Best column: " + bestColumn + " (" + bestMove.Score + ")");
            }
            return CurrentRoot.Score;
        }

        /// <summary>
        /// Print board state.
        /// </summary>
        public void PrintCurrentState()
        {
            CurrentRoot.BoardState.PrintBoard();
        }

        private void PrintIndents(int depth = 0)
        {
            while (depth > 0)
            {
                Console.Write("  ");
                depth--;
            }
        }
    } 

    /// <summary>
    /// GameState class represents one specific board state.
    /// </summary>
    [Serializable]
    public class GameState : ICloneable
    {
        // Board width.
        public int Width { get; private set; }

        // Board height.
        public int Height { get; private set; }

        // Last inserted token column.
        public int LastColumn = -1;

        // Last inserted token row.
        public int LastRow = -1;

        // Last player that inserted a token.
        public byte LastPlayer = 2;

        // Board is a 2D array.
        private byte[,] Board;

        /// <summary>
        /// Default dimensions (7 columns, 6 rows) constructor.
        /// </summary>
        public GameState()
        {
            CreateGame(7, 6);
        }

        /// <summary>
        /// State constructor.
        /// </summary>
        /// <param name="width">Number of columns.</param>
        /// <param name="height">Number of rows.</param>
        public GameState(int width, int height)
        {
            CreateGame(width, height);
        }

        private void CreateGame(int width, int height)
        {
            Width = width;
            Height = height;

            Board = new byte[height, width];

            // Initialize table to 0s.
            for (int i = 0; i < Height; i++)
                for (int j = 0; j < Width; j++)
                {
                    Board[i, j] = 0;
                }
        }

        /// <summary>
        /// Insert token to a random column.
        /// </summary>
        /// <param name="player"></param>
        public void InsertRandom(byte player = 2)
        {
            bool inserted = false;
            do
            {
                Random rng = new Random();
                int randomColumn = rng.Next(0, Width);
                inserted = Insert(randomColumn, player);
            }
            while (!inserted);
        }

        /// <summary>
        /// Insert token.
        /// </summary>
        /// <param name="column">Column number (starts from 0).</param>
        /// <param name="player">Player identifier. 1: human (default); 2: CPU.</param>
        /// <returns>True if inserted successfully.</returns>
        public bool Insert(int column, byte player = 1)
        {
            if (IsValidMove(column))
            {
                int insertRow = (Height - 1) - ColumnHeight(column);

                Board[insertRow, column] = player;
                LastColumn = column;
                LastRow = insertRow;
                LastPlayer = player;

                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Checks if insertion into column is possible.
        /// </summary>
        /// <param name="column">Column number (starts from 0).</param>
        /// <returns>True if valid move.</returns>
        public bool IsValidMove(int column)
        {
            if (column < 0 || column >= Width)
                return false;
            if (Board[0, column] != 0)
                return false;
            return true;
        }

        /// <summary>
        /// Filled column height.
        /// </summary>
        /// <param name="column">Column number (starts from 0).</param>
        /// <returns>Number of tokens in column.</returns>
        public int ColumnHeight(int column)
        {
            int currentColumnHeight = 0;
            for (int i = Height - 1; i >= 0; i--)
            {
                if (Board[i, column] != 0)
                    currentColumnHeight++;
                else
                    break;
            }
            return currentColumnHeight;
        }

        /// <summary>
        /// Checks current state (looking from last token insertion point).
        /// </summary>
        /// <returns></returns>
        public byte CheckState()
        {
            return CheckState(LastRow, LastColumn);
        }

        private byte CheckState(int fromRow, int fromColumn)
        {
            if (fromRow == -1 || fromColumn == -1)
                return 0;

            byte player = Board[fromRow, fromColumn];
            if (player == 0)
                return 0;

            // Check horizontal
            int numContinuous = 1;
            int spaceLeft = fromColumn - 1;
            int spaceRight = Width - 1 - fromColumn;
            int leftWalker = fromColumn - 1;
            int rightWalker = fromColumn + 1;
            while (leftWalker >= 0)
            {
                if (Board[fromRow, leftWalker] == player)
                {
                    numContinuous++;
                    leftWalker--;
                }
                else
                    break;
            }
            while (rightWalker < Width)
            {
                if (Board[fromRow, rightWalker] == player)
                {
                    numContinuous++;
                    rightWalker++;
                }
                else
                    break;
            }
            if (numContinuous >= 4)
            {
                // Return winning player number (1 human, 2 CPU).
                return player;
            }

            // Check vertical
            numContinuous = 1;
            int spaceUp = fromRow;
            int spaceDown = Height - 1 - fromRow;
            int upWalker = fromRow - 1;
            int downWalker = fromRow + 1;
            while (upWalker >= 0)
            {
                if (Board[upWalker, fromColumn] == player)
                {
                    numContinuous++;
                    upWalker--;
                }
                else
                    break;
            }
            while (downWalker < Height)
            {
                if (Board[downWalker, fromColumn] == player)
                {
                    numContinuous++;
                    downWalker++;
                }
                else
                    break;
            }
            if (numContinuous >= 4)
            {
                // Return winning player number.
                return player;
            }

            // Check topLeft-bottomRight diagonal.
            numContinuous = 1;
            int spaceUpLeft = Math.Min(fromRow, fromColumn);
            int spaceDownRight = Math.Min(Height - 1 - fromRow, Width - 1 - fromColumn);
            int upLeftLeftWalker = fromColumn - 1;
            int upLeftUpWalker = fromRow - 1;
            int downRightRightWalker = fromColumn + 1;
            int downRightDownWalker = fromRow + 1;
            while (upLeftLeftWalker >= 0 && upLeftUpWalker >= 0)
            {
                if (Board[upLeftUpWalker, upLeftLeftWalker] == player)
                {
                    numContinuous++;
                    upLeftUpWalker--;
                    upLeftLeftWalker--;
                }
                else
                    break;
            }
            while (downRightRightWalker < Width && downRightDownWalker < Height)
            {
                if (Board[downRightDownWalker, downRightRightWalker] == player)
                {
                    numContinuous++;
                    downRightRightWalker++;
                    downRightDownWalker++;
                }
                else
                    break;
            }
            if (numContinuous >= 4)
            {
                // Return winning player number.
                return player;
            }

            // Check topRight-bottomLeft diagonal.
            numContinuous = 1;
            int spaceUpRight = Math.Min(Width - 1 - fromColumn, fromRow);
            int spaceDownLeft = Math.Min(Height - 1 - fromRow, fromColumn);
            int upRightRightWalker = fromColumn + 1;
            int upRightUpWalker = fromRow - 1;
            int downLeftLeftWalker = fromColumn - 1;
            int downLeftDownWalker = fromRow + 1;
            while (upRightRightWalker < Width && upRightUpWalker >= 0)
            {
                if (Board[upRightUpWalker, upRightRightWalker] == player)
                {
                    numContinuous++;
                    upRightRightWalker++;
                    upRightUpWalker--;
                }
                else
                    break;
            }
            while (downLeftLeftWalker >= 0 && downLeftDownWalker < Height)
            {
                if (Board[downLeftDownWalker, downLeftLeftWalker] == player)
                {
                    numContinuous++;
                    downLeftLeftWalker--;
                    downLeftDownWalker++;
                }
                else
                    break;
            }
            if (numContinuous >= 4)
            {
                // Return winning player number.
                return player;
            }

            // No winning scenario found, return 0.
            return 0;
        }

        /// <summary>
        /// Print current board state.
        /// </summary>
        /// <param name="depth">Board depth in game tree (just for nicer formatting when printing whole game tree).</param>
        public void PrintBoard(int depth = 0, GameState g = null)
        {
            if (g == null)
            {
                g = this;
            }
            for (int i = 0; i < Height; i++)
            {
                PrintIndents(depth);
                for (int j = 0; j < Width; j++)
                {
                    if (g.Board[i, j] == 0)
                    {
                        // Empty
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write(".");
                        Console.ResetColor();
                    }
                    else if (g.Board[i, j] == 1)
                    {
                        // Player
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("X");
                        Console.ResetColor();
                    }
                    else if (g.Board[i, j] == 2)
                    {
                        // CPU
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("O");
                        Console.ResetColor();
                    }
                }
                Console.WriteLine();
            }
            PrintIndents(depth);
            for (int j = 0; j < g.Width; j++)
            {
                Console.Write(j);
            }
            Console.WriteLine();
        }

        private void PrintIndents(int depth = 0)
        {
            while (depth > 0)
            {
                Console.Write("  ");
                depth--;
            }
        }

        public object Clone()
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, this);
                ms.Position = 0;

                return (GameState)formatter.Deserialize(ms);
            }
        }
    }

    public static class Extensions
    {
        public static void Shuffle<T>(this IList<T> list)
        {
            Random rng = new Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}
