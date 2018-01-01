using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace TwoRegularRho
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // PUT YOUR CYCLE SIZES HERE
            int[] cycle_sizes = { 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };

            Graph g = new Graph(cycle_sizes);
            g.Initialize_Random_Labels();
            g.Solve();
            g.Print();
            Console.WriteLine(g.Validate()); // Independently validates the Rho Labeling
            Console.ReadKey();
        }
    }

    class Node
    {
        public int label;
        public Edge left;
        public Edge right;
    }

    class Edge
    {
        public int length;
        public Node left;
        public Node right;
    }

    class Graph
    {
        public Node[] node_heap;
        public Node[] head_heap;
        public Edge[] edge_heap;
        public List<Node> bad_node_list;
        public Dictionary<int, List<Node>> label_to_node_list;
        public Dictionary<int, List<Edge>> length_to_edge_list;
        public Random randy;
        public double update_size;
        public double p_of_accept_bad_update;
        public int K;

        public Graph(int[] cycle_sizes, double p_of_accept_bad_update = 0.0)
        {
            int i, j, nIndex, hIndex;
            int number_nodes = 0;
            for(i = 0; i < cycle_sizes.Length; i++)
            {
                Debug.Assert(cycle_sizes[i] >= 3);
                number_nodes += cycle_sizes[i];
            }

            // This update rule was chosen quickly from a few runtime experiements
            // It can likely be improved
            update_size = 1/(Math.Pow(number_nodes, 2.5)); 
            this.p_of_accept_bad_update = p_of_accept_bad_update;

            // Create Nodes and Edges
            node_heap = new Node[number_nodes];
            head_heap = new Node[cycle_sizes.Length];
            edge_heap = new Edge[number_nodes];

            nIndex = 0;
            hIndex = 0;
            for(i = 0; i < cycle_sizes.Length; i++)
            {
                for(j = 0; j < cycle_sizes[i]; j++)
                {
                    node_heap[nIndex] = new Node();
                    edge_heap[nIndex] = new Edge();
                    if (j == 0)
                    {
                        head_heap[hIndex] = node_heap[nIndex];
                        hIndex++;
                    }
                    nIndex++;
                }
            }

            // Connect Nodes and Edges
            nIndex = 0;
            for(i = 0; i < cycle_sizes.Length; i++)
            {
                for(j = 0; j < cycle_sizes[i]; j++)
                {
                    node_heap[nIndex].right = edge_heap[nIndex];
                    edge_heap[nIndex].left = node_heap[nIndex];
                    nIndex++;
                }
            }
            nIndex = 0;
            hIndex = 0;
            for(i = 0; i < cycle_sizes.Length; i++)
            {
                node_heap[nIndex].left = edge_heap[hIndex + cycle_sizes[i] - 1];
                nIndex++;
                for(j = 0; j < cycle_sizes[i]-1; j++)
                {
                    node_heap[nIndex].left = edge_heap[nIndex - 1];
                    nIndex++;
                }
                hIndex += cycle_sizes[i];
            }
            nIndex = 0;
            hIndex = 0;
            for(i = 0; i < cycle_sizes.Length; i++)
            {
                for(j = 0; j < cycle_sizes[i]-1; j++)
                {
                    edge_heap[nIndex].right = node_heap[nIndex + 1];
                    nIndex++;
                }
                edge_heap[nIndex].right = node_heap[hIndex];
                nIndex++;
                hIndex += cycle_sizes[i];
            }

            // Initialize everything else
            K = node_heap.Length * 2 + 1;
            bad_node_list = new List<Node>();
            label_to_node_list = new Dictionary<int, List<Node>>();
            for (i = 0; i < K; i++)
                label_to_node_list.Add(i, new List<Node>());
            length_to_edge_list = new Dictionary<int, List<Edge>>();
            for (i = 0; i <= number_nodes; i++)
                length_to_edge_list.Add(i, new List<Edge>());
            randy = new Random();
        }

        public void TestGraphConstruction()
        {
            int i;
            for(i = 0; i < node_heap.Length; i++)
            {
                node_heap[i].label = i;
                edge_heap[i].length = i;
            }
            Node node;
            StringBuilder sb = new StringBuilder();
            foreach(Node head in head_heap)
            {
                node = head;
                sb.Append("Node " + node.label + ":\t" + node.right.right.label + ", " + node.left.left.label + "\n");
                node = node.right.right;
                while(node != head)
                {
                    sb.Append("Node " + node.label + ":\t" + node.right.right.label + ", " + node.left.left.label + "\n");
                    node = node.right.right;
                }
            }
            Console.WriteLine(sb.ToString());
        }

        public void Initialize_Random_Labels()
        {
            foreach(Node node in node_heap)
            {
                node.label = randy.Next(0, K);
                Add_Node_To_List(node);
            }

            foreach(Edge edge in edge_heap)
            {
                edge.length = EdgeLength(edge.left.label, edge.right.label);
                Add_Edge_To_List(edge);
            }
        }

        public void Update()
        {
            Node node;
            int old_score, old_label, new_label;
            double p = p_of_accept_bad_update;

            bool done = false;
            while (!done)
            {
                node = bad_node_list[randy.Next(0, bad_node_list.Count)];
                old_label = node.label;
                old_score = bad_node_list.Count;

                new_label = randy.Next(0, K);
                Set_Node(node, new_label);

                if (bad_node_list.Count < old_score || randy.NextDouble() < p)
                    done = true;
                else
                {
                    Set_Node(node, old_label);
                    p += (1 - p) * update_size;
                }
            }
        }

        public int Get_Optimal_Label(Node node)
        {
            int best_score = bad_node_list.Count;
            int best_label = node.label;

            for(int i = 0; i < K; i++)
            {
                Set_Node(node, i);
                if(bad_node_list.Count < best_score)
                {
                    best_score = bad_node_list.Count;
                    best_label = i;
                }
            }
            return best_label;
        }

        public void Set_Node(Node node, int label)
        {
            Remove_Edge_From_List(node.left);
            Remove_Edge_From_List(node.right);
            Remove_Node_From_List(node);

            node.label = label;
            node.left.length = EdgeLength(node.left.left.label, node.label);
            node.right.length = EdgeLength(node.right.right.label, node.label);

            Add_Node_To_List(node);
            Add_Edge_To_List(node.left);
            Add_Edge_To_List(node.right);
        }
        

        public void Add_Node_To_List(Node node)
        {
            label_to_node_list[node.label].Add(node);

            if (label_to_node_list[node.label].Count > 1)
            {
                bad_node_list.Add(node);

                // Add the first element as well the first time this occurs
                if (label_to_node_list[node.label].Count == 2)
                    bad_node_list.Add(label_to_node_list[node.label][0]);
            }
        }

        public void Add_Edge_To_List(Edge edge)
        {
            length_to_edge_list[edge.length].Add(edge);

            if (edge.length == 0)
            {
                bad_node_list.Add(edge.left);
                bad_node_list.Add(edge.right);
            }
            else if (length_to_edge_list[edge.length].Count > 1)
            {
                bad_node_list.Add(edge.left);
                bad_node_list.Add(edge.right);

                if (length_to_edge_list[edge.length].Count == 2)
                {
                    bad_node_list.Add(length_to_edge_list[edge.length][0].left);
                    bad_node_list.Add(length_to_edge_list[edge.length][0].right);
                }
            }
        }

        public void Remove_Node_From_List(Node node)
        {
            label_to_node_list[node.label].Remove(node);

            if (label_to_node_list[node.label].Count > 0)
            {
                bad_node_list.Remove(node);
                if (label_to_node_list[node.label].Count == 1)
                    bad_node_list.Remove(label_to_node_list[node.label][0]);
            }
        }

        public void Remove_Edge_From_List(Edge edge)
        {
            length_to_edge_list[edge.length].Remove(edge);

            if(edge.length == 0)
            {
                bad_node_list.Remove(edge.left);
                bad_node_list.Remove(edge.right);
            }
            else if(length_to_edge_list[edge.length].Count > 0)
            {
                bad_node_list.Remove(edge.left);
                bad_node_list.Remove(edge.right);

                if(length_to_edge_list[edge.length].Count == 1)
                {
                    bad_node_list.Remove(length_to_edge_list[edge.length][0].left);
                    bad_node_list.Remove(length_to_edge_list[edge.length][0].right);
                }
            }
        }
        
        public int EdgeLength(int node_label_1, int node_label_2)
        {
            int label = Math.Abs(node_label_1 - node_label_2);
            return Math.Min(label, K - label);
        }

        public void Solve()
        {
            while (bad_node_list.Count > 0)
            {
                //Console.WriteLine(bad_node_list.Count);
                Update();
            }
        }

        public string Validate()
        {
            int i;
            bool[] node_label_flag = new bool[K];
            bool[] edge_length_flag = new bool[edge_heap.Length];

            for (i = 0; i < K; i++)
                node_label_flag[i] = false;
            for (i = 0; i < edge_heap.Length; i++)
                edge_length_flag[i] = false;

            foreach(Node node in node_heap)
            {
                if (node_label_flag[node.label])
                    return "Not Good!";
                node_label_flag[node.label] = true;
            }

            foreach(Edge edge in edge_heap)
            {
                if (edge_length_flag[edge.length-1])
                    return "Not Good!";
                edge_length_flag[edge.length-1] = true;
            }
            return "Validated!";
        }

        public void Print()
        {
            StringBuilder sb = new StringBuilder();
            int cycle_index = 1;
            int inner_index;
            Node node;

            foreach(Node head in head_heap)
            {
                node = head;
                inner_index = 1;
                sb.Append("Node " + cycle_index + "." + inner_index + ":\t" + node.label + "\n");
                inner_index++;
                node = node.right.right;
                while(node != head)
                {
                    sb.Append("Node " + cycle_index + "." + inner_index + ":\t" + node.label + "\n");
                    inner_index++;
                    node = node.right.right;
                }
                sb.Append("\n");
                cycle_index++;
            }
            if(bad_node_list.Count > 0)
            {
                sb.Append("\nBad Nodes:\n");
                foreach (Node bad_node in bad_node_list)
                    sb.Append(bad_node.label + " ");
            }
            
            sb.Append("\n");
            Console.WriteLine(sb.ToString());
        }
    }
}
