using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace GrainGrowth
{

    public partial class GrainGrowth : Form
    {
        //tables and lists
        int sizeX, sizeY;
        bool[,] state = null;
        bool[,] nextstate = null;
        
        int[,] spot = null;
        Color[,] color = null;
        Color[,] nextcolor = null;
        List<String> lines = null;
        List<Point> borderPoints = null;
        List<Point> allPoints = null;
        List<ColorCount> colorAmounts = null;
        List<ColorCount> mostFrequentList = null;
        List<Color> forbiddenColors = null;
        List<Color> tempColors = null;
        List<Color> eachColor = null;
        List<Color> safeColors = null;
        //graphics
        Color defaultColor, inclusionColor;
        Random _rand;
        Graphics g;
        Bitmap bmp;
        //SolidBrush b;

        //misc
        Thread mcThread;
        System.Windows.Forms.Timer t;
        Color randomColor;
        /*, mostFrequentList*/
        uint table_iter, rand_num;
        int rand_x, rand_y, spotAmount, mcCurrentStep, mcCheckedCells;
        bool isFinished, isActive;
        bool[] currentNeigh, mooreNeigh, nearestNeigh, furtherNeigh;
        Color mostFrequent = new Color();
        bool foundColor = false;

        public class ColorCount
        {
            public Color c;
            public int amount;

            public ColorCount(Color c, int amount)
            {
                this.c = c;
                this.amount = amount;
            }   
        }

        //inits
        public GrainGrowth()
        {
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            InitializeComponent();    
            //init stuff that can or could only be done once
            g = DrawingBoard.CreateGraphics();
            defaultColor = Color.White;
            inclusionColor = Color.Black;
            _rand = new Random();
            t = new System.Windows.Forms.Timer
            {
                Interval = 15
            };
            t.Tick += new EventHandler(Btn_Next_Click);
            mooreNeigh = new bool[]
            {
                true, true, true,
                true, false, true,
                true, true, true
            };
            nearestNeigh = new bool[]
            {
                false, true, false,
                true, false, true,
                false, true, false
            };
            furtherNeigh = new bool[]
            {
                true, false, true,
                false, false, false,
                true, false, true
            };

            ColumnHeader header = new ColumnHeader
            {
                Text = "Grains",
                Name = "col1",
                Width = 50
            };
            //header.Height = 50;
            lv_SelectedPoints.Columns.Add(header);

            //init function - can be initiated more than once
            InitStuff(); 
        }
        private void InitStuff()
        {
            //init tables and lists
            sizeX = DrawingBoard.Width;
            sizeY = DrawingBoard.Height;
            lines = new List<String>();
            borderPoints = new List<Point>();
            colorAmounts = new List<ColorCount>();
            allPoints = new List<Point>();
            GenerateNewPoints();

            state = new bool[sizeX, sizeY];
            color = new Color[sizeX, sizeY];

            nextstate = new bool[sizeX, sizeY];
            nextcolor = new Color[sizeX, sizeY];

            spot = new int[sizeX, sizeY];

            //init gfx
            bmp = new Bitmap(sizeX, sizeY);
            //b = new SolidBrush(defaultColor);
            ClearBoard(); 

            //init misc
            isFinished = false;
            isActive = false;
            currentNeigh = new bool[] { false, false, false, false, false, false, false, false, false };
            table_iter = 0;
            spotAmount = 0;
            mcCurrentStep = 0;
            mcCheckedCells = 0;
            lab_MCCurrentStep.Text = "Finished steps: 0";
            lab_MCCheckedCells.Text = "Checked cells: 0";
            tempColors = new List<Color>();
            eachColor = new List<Color>();
            mostFrequentList = new List<ColorCount>();
            forbiddenColors = new List<Color>();
            safeColors = new List<Color>();
            //lb_eachColor.Items.Clear();
            //lb_safeColors.Items.Clear();
        }

        //simple moore
        private void AlgSimpleMoore()
        {
            for (int i = 0; i < sizeX; i++)
                for (int j = 0; j < sizeY; j++) //global cell field
                {
                    if (!state[i, j])
                    {
                        FindNeighbors(mooreNeigh, i, j);
                        SetState(i, j);
                    }
                }
        }
        private void SetState(int a, int b)
        {
            if (Convert.ToBoolean(tempColors.Count)) // if there's at least one color in the neighborhood
            {
                FindColorFrequencies(); //create a list of color frequencies
                FindMostFrequent(); //find a list of most frequent
                mostFrequent = mostFrequentList[_rand.Next(0, mostFrequentList.Count - 1)].c;
                nextstate[a, b] = true; //change the next states
                nextcolor[a, b] = mostFrequent; //randomly gets the most frequent color from FindColorFrequencies method if more than 1 color
                //Color mode = tempColors.GroupBy(v => v).OrderByDescending(g => g.Count()).First().Key; //sort the colors and get the most frequent
                colorAmounts.Clear();
                mostFrequentList.Clear();
            }
            tempColors.Clear();
        }
        private void FindNeighbors(bool[] neigh, int a, int b)
        {
            currentNeigh = neigh;
            for (int m = a - 1; m <= a + 1; m++)
                for (int n = b - 1; n <= b + 1; n++) // local cell field
                {
                    if (m >= 0 && m < sizeX && n >= 0 && n < sizeY)
                        if (currentNeigh[table_iter])
                            if (state[m, n])
                                tempColors.Add(color[m, n]);
                    table_iter++;
                }
            table_iter = 0;
            tempColors.RemoveAll(c => c.Equals(defaultColor)); //delete ungrown spaces
            tempColors.RemoveAll(c => c.Equals(inclusionColor)); //delete inclusions
            foreach (Color c in forbiddenColors)
                tempColors.RemoveAll(d => d.Equals(c)); //remove forbidden neighbors
        }
        private void FindColorFrequencies() //creates a list of color-frequency pairs
        {
            bool alreadyFound = false;
            foreach (Color c in tempColors) //create a list of color frequencies from neighboring colors
            {
                foreach (ColorCount ca in colorAmounts)
                {
                    if (c == ca.c)
                    {
                        ca.amount++;
                        alreadyFound = true;
                    }
                }
                if (alreadyFound == false)
                    colorAmounts.Add(new ColorCount(c, 1));
                else
                    alreadyFound = false;
            }
        }
        private void FindMostFrequent() //finds the color and frequency of it
        {
            ColorCount mostFrequent = new ColorCount(new Color(), 0);
            foreach (ColorCount cc in colorAmounts) //find the most frequent color in the color freq list
                if (cc.amount > mostFrequent.amount)
                    mostFrequent = cc;

            foreach (ColorCount cc in colorAmounts) // if there's more than one most frequent at the same time, find colors with the same frequency
                if (cc.amount == mostFrequent.amount)
                    mostFrequentList.Add(cc);
        }

        //advanced moore
        private void AlgAdvancedMoore()
        { 
            for (int i = 0; i < sizeX; i++)
                for(int j = 0; j < sizeY; j++)
                {
                    if(!state[i,j])
                    {
                        //rule 1 start
                        FindNeighbors(mooreNeigh, i, j);
                        SetStateByRange(i, j, 5, 8);
                        //rule 1 end

                        //rule 2 start
                        if (foundColor == false)
                        {
                            FindNeighbors(nearestNeigh, i, j);
                            SetStateByRange(i, j, 3, 4);
                        }
                        //rule 2 end

                        //rule 3 start
                        if (foundColor == false)
                        {
                            FindNeighbors(furtherNeigh, i, j);
                            SetStateByRange(i, j, 3, 4);
                        }
                        //rule 3 end

                        //rule 4 start
                        if (foundColor == false)
                        {
                            FindNeighbors(mooreNeigh, i, j);
                            SetStateByProbability(i, j, Convert.ToInt32(tb_Probability.Text.ToString()));
                        }
                        //rule 4 end
                    }
                    foundColor = false; //reset color bool after each cell
                }
        }
        private void SetStateByRange(int a, int b, int left, int right)
        {
            if (Convert.ToBoolean(tempColors.Count))
            {
                FindColorFrequencies();
                FindMostFrequent();

                foreach (ColorCount cc in mostFrequentList) //we presume we will find only one color at most
                    if (cc.amount >= left && cc.amount <= right)
                    {
                        mostFrequent = cc.c;
                        foundColor = true;
                    }
                if (foundColor == true)
                {
                    nextstate[a, b] = true;
                    nextcolor[a, b] = mostFrequent;
                }
                colorAmounts.Clear();
                mostFrequentList.Clear();
            }
            tempColors.Clear();
        }
        private void SetStateByProbability(int a, int b, int probability)
        {
            if (Convert.ToBoolean(tempColors.Count))
            {
                FindColorFrequencies();
                FindMostFrequent();
                int randNum = _rand.Next(0, 101); //101 because it will never be 100
                if (randNum <= probability)
                {
                    mostFrequent = mostFrequentList[_rand.Next(0, mostFrequentList.Count - 1)].c; //randomly gets the most frequent color from FindColorFrequencies method if more than 1 color
                    nextstate[a, b] = true; //change the next states
                    nextcolor[a, b] = mostFrequent;
                }
                colorAmounts.Clear();
                mostFrequentList.Clear();
            }
            tempColors.Clear();
        }

        //borders
        private void FindBorderNeighbor(bool[] neigh, int a, int b, bool isMC)
        {
            for (int m = a - 1; m <= a + 1; m++)
                for (int n = b - 1; n <= b + 1; n++)
                {
                    if (m >= 0 && m < sizeX && n >= 0 && n < sizeY)
                        if (neigh[table_iter])
                        {
                            if(isMC) // if we're running monte carlo
                            {
                                if (forbiddenColors.Contains(color[m, n]))
                                    continue;
                                else
                                    tempColors.Add(color[m, n]);
                            }
                            else
                            {
                                tempColors.Add(color[m, n]);
                            }
                        }         
                    table_iter++;
                }
            table_iter = 0;
            if(isMC == false)
                tempColors = tempColors.Distinct().ToList();
        }
        private void BorderEveryDetection()
        {
            for (int i = 0; i < sizeX; i++)
                for (int j = 0; j < sizeY; j++)
                {
                    FindBorderNeighbor(mooreNeigh, i, j, false);
                    SetEveryBordering(i, j);
                }
        }
        private void SetEveryBordering(int a, int b)
        {
            if (tempColors.Count >= 2)
                nextcolor[a, b] = inclusionColor;
            tempColors.Clear();
        }

        private void Rb_BordersEvery_CheckedChanged(object sender, EventArgs e)
        {
            if (rb_BordersEvery.Checked)
            {
                if (rad_Click.Checked)
                {
                    rad_Click.Checked = false;
                    rad_Random.Checked = true;
                }

                if (rb_MicroSubs.Checked)
                {
                    rb_MicroSubs.Checked = false;
                    rb_MicroNone.Checked = true;
                }
                else if (rb_MicroDual.Checked)
                {
                    rb_MicroDual.Checked = false;
                    rb_MicroNone.Checked = true;
                }
            }
        }
        private void BorderSingleDetection(Color c)
        {
            

            for (int i = 0; i < sizeX; i++)
                for (int j = 0; j < sizeY; j++)
                {
                    FindBorderNeighbor(mooreNeigh, i, j, false);
                    SetSingleBordering(i, j, c);
                }
            
        }
        private void SetSingleBordering(int a, int b, Color selected)
        {
            if (tempColors.Count >= 2)
            {
                bool containsSafeColors = false;
                foreach (Color c in tempColors)
                    foreach (Color ci in safeColors)
                        if (c == ci)
                            containsSafeColors = true;
                if (containsSafeColors)
                    nextcolor[a, b] = selected;
            }
            tempColors.Clear();
        }

        private void Rb_BordersSingle_CheckedChanged(object sender, EventArgs e)
        {
            if (rb_BordersSingle.Checked)
            {
                if (rad_Click.Checked)
                {
                    rad_Click.Checked = false;
                    rad_Random.Checked = true;
                }

                if (rb_MicroSubs.Checked)
                {
                    rb_MicroSubs.Checked = false;
                    rb_MicroNone.Checked = true;
                }
                else if (rb_MicroDual.Checked)
                {
                    rb_MicroDual.Checked = false;
                    rb_MicroNone.Checked = true;
                }
            }
        }
        private void BorderExclusiveDetection(Color c)
        {
            //Color c;
            //if (cb_BordersUnique.Checked)
            //{
            //    do c = Color.FromArgb(_rand.Next(256), _rand.Next(256), _rand.Next(256));
            //    while (c == defaultColor && c == inclusionColor && eachColor.Contains(c));
            //}
            //else c = inclusionColor;
            safeColors.Add(c);
            for (int i = 0; i < sizeX; i++)
                for (int j = 0; j < sizeY; j++)
                {
                    FindBorderNeighbor(mooreNeigh, i, j, false);
                    SetExclusiveBordering(i, j, c);
                }
            //safeColors.Clear();
        }
        private void SetExclusiveBordering(int a, int b, Color selected)
        {
            if (tempColors.Count >= 2)
            {
                bool containsOnlySafeColors = false;
                foreach (Color c in tempColors) //every color in the neighborhood
                {
                    if (safeColors.Contains(c))
                        containsOnlySafeColors = true;
                    else
                    {
                        containsOnlySafeColors = false;
                        break;
                    }            
                }
                if (containsOnlySafeColors)
                        nextcolor[a, b] = selected;      
            }
            tempColors.Clear();
        }

        private void Rb_BordersExclusive_CheckedChanged(object sender, EventArgs e)
        {
            if (rb_BordersExclusive.Checked)
            {
                if (rad_Click.Checked)
                {
                    rad_Click.Checked = false;
                    rad_Random.Checked = true;
                }

                if (rb_MicroSubs.Checked)
                {
                    rb_MicroSubs.Checked = false;
                    rb_MicroNone.Checked = true;
                }
                else if (rb_MicroDual.Checked)
                {
                    rb_MicroDual.Checked = false;
                    rb_MicroNone.Checked = true;
                }
            }
        }
        private void Btn_BordersAdd_Click(object sender, EventArgs e)
        {
            Color c;
            if (cb_BordersUnique.Checked)
            {
                do c = Color.FromArgb(_rand.Next(256), _rand.Next(256), _rand.Next(256));
                while (c == defaultColor && c == inclusionColor && eachColor.Contains(c));
            }
            else c = inclusionColor;

            for (int i = 0; i < Convert.ToInt32(tb_BordersSize.Value); i++)
            {
                if (rb_BordersEvery.Checked)
                {
                    BorderEveryDetection();
                    WriteBoard();
                }
                else if (rb_BordersSingle.Checked)
                {
                    BorderSingleDetection(c);
                    WriteBoard();     
                }
                else if (rb_BordersExclusive.Checked)
                {
                    BorderExclusiveDetection(c);
                    WriteBoard();
                }
            }
            safeColors.Clear();
            lv_SelectedPoints.Items.Clear();
        }
        private void Btn_BordersClear_Click(object sender, EventArgs e)
        {
            /*for (int i = 0; i < sizeX; i++)
                for (int j = 0; j < sizeY; j++)
                {
                    if (nextcolor[i, j] != inclusionColor)
                    {
                        nextcolor[i, j] = defaultColor;
                        nextstate[i, j] = false;
                    }

                }*/
            for (int i = eachColor.Count - 1; i >= 0; i--)
                DeleteColorFromBoard(eachColor[i]);
            WriteBoard();
        }

        //inclusions
        private void Btn_AddInc_Click(object sender, EventArgs e)
        {
            int inclusion_amount, inclusion_size;
            inclusion_amount = Convert.ToInt32(tb_IncAmount.Text);
            inclusion_size = Convert.ToInt32(tb_IncSize.Text);
            Point middle, current;
            if (rb_IncTimeAfter.Checked) //if we add inclusions after, generate a list of border points for this step
            {
                for (int i = 0; i < sizeX; i++)
                    for (int j = 0; j < sizeY; j++)
                    {
                        for (int m = i - 1; m <= i + 1; m++)
                            for (int n = j - 1; n <= j + 1; n++) // local cell field
                            {
                                /*if (cb_Periodic.Checked) //if periodic
                                {
                                    if (currentNeigh[table_iter])
                                        tempColors.Add(color[Mod(m, sizeX), Mod(n, sizeY)]);
                                }
                                else //if not periodic
                                {*/
                                if (m >= 0 && m < sizeX && n >= 0 && n < sizeY)
                                    if (currentNeigh[table_iter])
                                        tempColors.Add(color[m, n]);
                                //}
                                table_iter++;
                            }
                        //tempColors.RemoveAll(c => c.Equals(defaultColor)); //get unique colors
                        tempColors.RemoveAll(c => c.Equals(inclusionColor)); //delete inclusions
                        //upper two aren't needed for now
                        tempColors = tempColors.Distinct().ToList();
                        if (tempColors.Count > 1) //more than one color in the neighborhood means the cell is on border
                            borderPoints.Add(new Point(i, j)); //cell is on border - add it to the list
                        tempColors.Clear();
                        table_iter = 0;
                    }
            }

            for (int i = 0; i < inclusion_amount; i++) //
            {
                //isColliding = false;
                rand_x = _rand.Next(0, sizeX - 1);
                rand_y = _rand.Next(0, sizeY - 1);
                if (rb_IncSquare.Checked) //kwadraty
                {
                    if (rb_IncTimeBefore.Checked) //jesli przed rozrostem
                    {
                        for (int m = rand_x; m < rand_x + Convert.ToInt32(tb_IncSize.Text); m++)
                            for (int n = rand_y; n < rand_y + Convert.ToInt32(tb_IncSize.Text); n++)
                            {
                                if (m >= 0 && m < sizeX && n >= 0 && n < sizeY)
                                {
                                    if (state[m, n] == false)
                                    {
                                        nextstate[m, n] = true;
                                        nextcolor[m, n] = Color.Black;
                                    }
                                }
                            }
                    }
                    else //jesli po rozroscie
                    {
                        int rand_pt = _rand.Next(0, borderPoints.Count - 1);
                        for (int m = borderPoints[rand_pt].X; m < borderPoints[rand_pt].X + Convert.ToInt32(tb_IncSize.Text); m++)
                            for (int n = borderPoints[rand_pt].Y; n < borderPoints[rand_pt].Y + Convert.ToInt32(tb_IncSize.Text); n++)
                            {
                                if (m >= 0 && m < sizeX && n >= 0 && n < sizeY)
                                {
                                    nextstate[m, n] = true;
                                    nextcolor[m, n] = Color.Black;
                                }
                            }
                        borderPoints.RemoveAt(rand_pt);
                    }
                }
                else //kola
                {
                    if (rb_IncTimeBefore.Checked)
                    {
                        middle = new Point(rand_x, rand_y);
                        for (int m = rand_x - inclusion_size; m < rand_x + inclusion_size + 1; m++)
                            for (int n = rand_y - inclusion_size; n < rand_y + inclusion_size + 1; n++)
                            {
                                current = new Point(m, n);
                                if (m >= 0 && m < sizeX && n >= 0 && n < sizeY)
                                {
                                    if (Math.Sqrt(((current.X - middle.X) * (current.X - middle.X)) + ((current.Y - middle.Y) * (current.Y - middle.Y))) <= Convert.ToInt32(tb_IncSize.Text))
                                    {

                                        nextstate[m, n] = true;
                                        nextcolor[m, n] = Color.Black;
                                    }
                                }
                            }
                    }
                    else
                    {
                        int rand_pt = _rand.Next(0, borderPoints.Count - 1);
                        for (int m = borderPoints[rand_pt].X - inclusion_size; m < borderPoints[rand_pt].X + inclusion_size + 1; m++)
                            for (int n = borderPoints[rand_pt].Y - inclusion_size; n < borderPoints[rand_pt].Y + inclusion_size + 1; n++)
                            {
                                current = new Point(m, n);
                                if (m >= 0 && m < sizeX && n >= 0 && n < sizeY)
                                {
                                    if (Math.Sqrt(((current.X - borderPoints[rand_pt].X) * (current.X - borderPoints[rand_pt].X)) + ((current.Y - borderPoints[rand_pt].Y) * (current.Y - borderPoints[rand_pt].Y))) <= Convert.ToInt32(tb_IncSize.Text))
                                    {

                                        nextstate[m, n] = true;
                                        nextcolor[m, n] = Color.Black;
                                    }
                                }
                            }
                        borderPoints.RemoveAt(rand_pt);
                    }
                }
            }
            WriteBoard();
            borderPoints.Clear();
        }

        //microstructures
        private void Btn_MicroInsert_Click(object sender, EventArgs e)
        {
            if (rb_MicroSubs.Checked)
            {
                int grainsPerHole = Convert.ToInt32(tb_AmountPerHole.Value);
                //insert X amount of grains for each hole
                //if two holes become one, the density of grains per hole remains the same

                int grainsInCurrentHole = 0;
                int currentspotAmount = spotAmount;
                if (spotAmount == 0)
                    return;
                for (int i = currentspotAmount; i >= 1; i--)
                {
                    while (grainsInCurrentHole < grainsPerHole)
                    {
                        rand_x = _rand.Next(0, sizeX - 1);
                        rand_y = _rand.Next(0, sizeY - 1);
                        if (spot[rand_x, rand_y] == i) //we assume hole is empty/ungrown so no need for state check
                        {
                            AddNewUniqueGrain(rand_x, rand_y);
                            grainsInCurrentHole++;
                        }
                    }
                    grainsInCurrentHole = 0;
                    for (int g = 0; g < sizeX; g++)
                        for (int h = 0; h < sizeY; h++)
                        {
                            if (spot[g, h] == i)
                                spot[g, h] = 0;
                        }
                    spotAmount--;
                }
            }

        }
        private void Btn_MicroDualClear_Click(object sender, EventArgs e)
        {
            if (rb_MicroDual.Checked)
            {
                eachColor.Clear(); //experimental line
                do randomColor = Color.FromArgb(_rand.Next(256), _rand.Next(256), _rand.Next(256));
                while (randomColor == defaultColor && randomColor == inclusionColor && eachColor.Contains(randomColor));
                forbiddenColors.Add(randomColor);
                for (int i = 0; i < sizeX; i++)
                    for (int j = 0; j < sizeY; j++)
                        if (color[i, j] != Color.White)
                            nextcolor[i, j] = randomColor;
                WriteBoard();
            }
        }
        private void Rb_MicroSubs_CheckedChanged(object sender, EventArgs e)
        {
            if (rb_MicroSubs.Checked)
            {
                if (rad_Click.Checked)
                {
                    rad_Click.Checked = false;
                    rad_Random.Checked = true;
                }

                if (rb_BordersEvery.Checked)
                {
                    rb_BordersEvery.Checked = false;
                    rb_BordersNone.Checked = true;
                }
                else if (rb_BordersSingle.Checked)
                {
                    rb_BordersSingle.Checked = false;
                    rb_BordersNone.Checked = true;
                }
                else if (rb_BordersExclusive.Checked)
                {
                    rb_BordersExclusive.Checked = false;
                    rb_BordersNone.Checked = true;
                }
            }
        }
        private void Rb_MicroDual_CheckedChanged(object sender, EventArgs e)
        {
            if (rb_MicroDual.Checked)
            {
                if (rad_Click.Checked)
                {
                    rad_Click.Checked = false;
                    rad_Random.Checked = true;
                }

                if (rb_BordersEvery.Checked)
                {
                    rb_BordersEvery.Checked = false;
                    rb_BordersNone.Checked = true;
                }
                else if (rb_BordersSingle.Checked)
                {
                    rb_BordersSingle.Checked = false;
                    rb_BordersNone.Checked = true;
                }
                else if (rb_BordersExclusive.Checked)
                {
                    rb_BordersExclusive.Checked = false;
                    rb_BordersNone.Checked = true;
                }
            }
        }
        private void DeleteColorFromBoard(Color c)
        {
            if (c != defaultColor)//cannot be white
            {
                if(eachColor.Contains(c))
                    eachColor.Remove(c);
                /*for (int i = lb_eachColor.Items.Count - 1; i >= 0; --i)
                    if (lb_eachColor.Items[i].Text == HexConverter(c))
                        lb_eachColor.Items[i].Remove();*/
                spotAmount++; //each deleted grain is a different hole
                for (int i = 0; i < sizeX; i++)
                    for (int j = 0; j < sizeY; j++)
                    {
                        if (color[i, j] == c)
                        {
                            nextcolor[i, j] = defaultColor;
                            nextstate[i, j] = false;
                            spot[i, j] = spotAmount;

                            for (int m = i - 1; m <= i + 1; m++)
                                for (int n = j - 1; n <= j + 1; n++)
                                {
                                    if (m >= 0 && m < sizeX && n >= 0 && n < sizeY)
                                        if (color[m, n] != defaultColor)
                                            if (!forbiddenColors.Contains(color[m, n]))
                                                forbiddenColors.Add(color[m, n]);
                                }
                        }
                    }
                if (forbiddenColors.Contains(c))//release the color that was forbidden before it was deleted
                    forbiddenColors.Remove(c);
            }
        }

        //input
        private void AddNewUniqueGrain(int x, int y)
        {
            do randomColor = Color.FromArgb(_rand.Next(256), _rand.Next(256), _rand.Next(256));
            while (randomColor == defaultColor && randomColor == inclusionColor && eachColor.Contains(randomColor));
            state[x, y] = nextstate[x, y] = true; // changes ungrown into grown
            color[x, y] = nextcolor[x, y] = randomColor;
            eachColor.Add(randomColor);
            bmp.SetPixel(x, y, color[x, y]);
            //lb_eachColor.Items.Add(HexConverter(randomColor));
            //lb_eachColor.Items[lb_eachColor.Items.Count - 1].BackColor = randomColor;
            DrawingBoard.Image = bmp;
        }
        private void Btn_Insert_Click(object sender, EventArgs e)
        {
            //checkEnableControlButtons();
            if (rad_Random.Checked)
            {
                rand_num = Convert.ToUInt32(tb_Random.Text);
                if (rand_num > sizeX * sizeY)
                    return;
                for (int i = 0; i < rand_num; i++)
                {
                    //isColliding = false;
                    rand_x = _rand.Next(0, sizeX - 1);
                    rand_y = _rand.Next(0, sizeY - 1);

                    if (!state[rand_x, rand_y])
                    {
                        AddNewUniqueGrain(rand_x, rand_y);
                        if (!state.Cast<bool>().Contains(false))
                            break;
                    }
                    else
                        i--;
                }
            }
        }
        private void Rad_Click_CheckedChanged(object sender, EventArgs e)
        {
            if (rad_Click.Checked)
            {
                if (rb_MicroSubs.Checked)
                {
                    rb_MicroSubs.Checked = false;
                    rb_MicroNone.Checked = true;
                }
                else if (rb_MicroDual.Checked)
                {
                    rb_MicroDual.Checked = false;
                    rb_MicroNone.Checked = true;
                }

                if (rb_BordersEvery.Checked)
                {
                    rb_BordersEvery.Checked = false;
                    rb_BordersNone.Checked = true;
                }
                else if (rb_BordersSingle.Checked)
                {
                    rb_BordersSingle.Checked = false;
                    rb_BordersNone.Checked = true;
                }
                else if (rb_BordersExclusive.Checked)
                {
                    rb_BordersExclusive.Checked = false;
                    rb_BordersNone.Checked = true;
                }
            }

            //if
        }
        private void Rad_Random_CheckedChanged(object sender, EventArgs e)
        {
            if (rad_Random.Checked)
            {
                tb_Random.Enabled = true;
                if (!isFinished)
                    btn_Insert.Enabled = true;
            }

            else
            {
                tb_Random.Enabled = false;
                btn_Insert.Enabled = false;
            }
        }

        //growth control
        private void Btn_Next_Click(object sender, EventArgs e)
        {
            if(CheckDisableControlButtons())
                return;
            if (rb_SimpleMoore.Checked)
                AlgSimpleMoore();
            else
                AlgAdvancedMoore();
            if (nextstate != state)
                WriteBoard();
            else
                Btn_StartStop_Click(null, null);   
        }
        private void Btn_StartStop_Click(object sender, EventArgs e)
        {
            t.Tick += new EventHandler(Btn_Next_Click);
            if (t.Enabled) //if it's growing right now
            {
                t.Stop(); //then stop
                lb_Running.Text = "Not running";
            }
            else // if it's not growing
            {
                t.Start(); // then start
                lb_Running.Text = "Running";
            }
        }
        private void Btn_Reset_Click(object sender, EventArgs e)
        {
            StopUnfinish();
            /*btn_Next.Enabled = false;
            btn_StartStop.Enabled = false;
            btn_Reset.Enabled = false;
            btn_Insert.Enabled = true;*/
            InitStuff();
            //ClearBoard();
        }
        private void Btn_Reshape_Click(object sender, EventArgs e)
        {
            StopUnfinish();
            sizeX = Convert.ToInt32(tb_imgWidth.Text.ToString());
            sizeY = Convert.ToInt32(tb_imgHeight.Text.ToString());
            Size size = new Size(Convert.ToInt32(sizeX), Convert.ToInt32(sizeY));
            DrawingBoard.Size = size;
            InitStuff();
        }
        private void DrawingBoard_MouseDown(object sender, MouseEventArgs e)
        {
            if (rad_Click.Checked) // if cell is grown, do not ungrow it
            {
                if (!state[e.X, e.Y])
                    AddNewUniqueGrain(e.X, e.Y);
                //checkEnableControlButtons();  
            }
            else if (rb_MicroSubs.Checked || rb_MicroDual.Checked)
            {
                Color c = bmp.GetPixel(e.X, e.Y);
                DeleteColorFromBoard(c);
                WriteBoard();
            }
            else if (rb_BordersSingle.Checked)
            {
                Color c = bmp.GetPixel(e.X, e.Y);
                if (!safeColors.Contains(c))
                    safeColors.Add(c);
                else
                    safeColors.Remove(c);
                lv_SelectedPoints.Items.Clear();
                {
                    foreach (Color ci in safeColors)
                    {
                        lv_SelectedPoints.Items.Add(new ListViewItem(HexConverter(ci)));
                        lv_SelectedPoints.Items[lv_SelectedPoints.Items.Count - 1].BackColor = ColorTranslator.FromHtml(HexConverter(ci));
                    }  
                }
            }
            else if (rb_BordersExclusive.Checked)
            {
                Color c = bmp.GetPixel(e.X, e.Y);
                if (!safeColors.Contains(c))
                    safeColors.Add(c);
                else
                    safeColors.Remove(c);
                lv_SelectedPoints.Items.Clear();
                {
                    foreach (Color ci in safeColors)
                    {
                        lv_SelectedPoints.Items.Add(new ListViewItem(HexConverter(ci)));
                        lv_SelectedPoints.Items[lv_SelectedPoints.Items.Count - 1].BackColor = ColorTranslator.FromHtml(HexConverter(ci));
                    }
                }
            }
        }
        private void Stop()
        {
            t.Stop();
            //btn_Next.Enabled = true;
        }
        private void StopUnfinish()
        {
            isFinished = false;
            if (t.Enabled)
                t.Stop();
        }

        //import
        private void FromBMPToolStripMenuItem_Click(object sender, EventArgs e) 
        {
            OpenFileDialog fromBMP = new OpenFileDialog();
            if (fromBMP.ShowDialog() == DialogResult.OK)
            {
                Bitmap imgBMP = new Bitmap(fromBMP.FileName);
                sizeX = imgBMP.Width;//DrawingBoard.Width;
                sizeY = imgBMP.Height;//DrawingBoard.Height;
                lines = new List<String>();
                state = new bool[sizeX, sizeY];
                color = new Color[sizeX, sizeY];
                nextstate = new bool[sizeX, sizeY];
                nextcolor = new Color[sizeX, sizeY];
                currentNeigh = new bool[] { false, false, false, false, false, false, false, false, false };
                table_iter = 0;
                tempColors = new List<Color>();
                eachColor = new List<Color>();
                allPoints = new List<Point>();
                bmp = imgBMP;
                for (int i = 0; i < sizeX; i++)
                    for (int j = 0; j < sizeY; j++)
                    {
                        if (HexConverter(bmp.GetPixel(i, j)) == "#FFFFFF") //if it's white (ungrown)
                            state[i, j] = nextstate[i, j] = false;
                        else
                            state[i, j] = nextstate[i, j] = true;
                        color[i, j] = nextcolor[i, j] = bmp.GetPixel(i, j);
                        if (!eachColor.Contains(bmp.GetPixel(i, j)))
                            eachColor.Add(bmp.GetPixel(i, j));
                        allPoints.Add(new Point(i, j));
                    }
                DrawingBoard.Image = bmp;
                Size size = new Size(Convert.ToInt32(sizeX), Convert.ToInt32(sizeY));
                DrawingBoard.Size = size;
                mcCurrentStep = 0;
                mcCheckedCells = 0;
                lab_MCCurrentStep.Text = "Finished steps: 0";
                lab_MCCheckedCells.Text = "Checked cells: 0";
            }
        }
        private void FromTXTToolStripMenuItem_Click(object sender, EventArgs e) 
        {
            lines.Clear();
            char delimiter = ' ';
            t.Stop();
            OpenFileDialog fromTXT = new OpenFileDialog();
            if(fromTXT.ShowDialog() == DialogResult.OK)
            {
                using (var streamReader = new StreamReader(fromTXT.FileName))
                {
                    string line;
                    line = streamReader.ReadLine();
                    string[] sizes = line.Split(delimiter);
                    sizeX = Convert.ToInt32(sizes[0]);
                    sizeY = Convert.ToInt32(sizes[1]);
                    Size size = new Size(sizeX, sizeY);
                    DrawingBoard.Size = size;
                    state = new bool[sizeX, sizeY];
                    color = new Color[sizeX, sizeY];
                    nextstate = new bool[sizeX, sizeY];
                    nextcolor = new Color[sizeX, sizeY];
                    currentNeigh = new bool[] { false, false, false, false, false, false, false, false, false };
                    table_iter = 0;
                    tempColors = new List<Color>();
                    eachColor = new List<Color>();
                    allPoints = new List<Point>();
                    while ((line = streamReader.ReadLine()) != null)
                    {
                        string[] words = line.Split(delimiter);
                        int i = Convert.ToInt32(words[0]);
                        int j = Convert.ToInt32(words[1]);
                        Color colorr = ColorTranslator.FromHtml(words[2]);
                        if (words[2] == "#FFFFFF") //if it's white (ungrown)
                            nextstate[i, j] = false;
                        else
                            nextstate[i, j] = true;
                        nextcolor[i, j] = colorr;
                        if (!eachColor.Contains(colorr))
                            eachColor.Add(colorr);
                        allPoints.Add(new Point(i, j));
                    }
                    
                }
                mcCurrentStep = 0;
                mcCheckedCells = 0;
                lab_MCCurrentStep.Text = "Finished steps: 0";
                lab_MCCheckedCells.Text = "Checked cells: 0";
                WriteBoard();
            }
        }

        //export
        private void ToBMPToolStripMenuItem_Click(object sender, EventArgs e) 
        {
            SaveFileDialog toBMP = new SaveFileDialog
            {
                FileName = "cells.bmp",
                Filter = "Bitmap | *.bmp"
            };
            System.Drawing.Imaging.ImageFormat imgf = System.Drawing.Imaging.ImageFormat.Bmp;
            if(toBMP.ShowDialog() == DialogResult.OK)
                DrawingBoard.Image.Save(toBMP.FileName, imgf);
        }
        private void ToTXTToolStripMenuItem_Click(object sender, EventArgs e) 
        {
            SaveFileDialog toTXT = new SaveFileDialog
            {
                FileName = "cells.txt",
                Filter = "Text file | *.txt"
            };
            lines.Clear();
            lines.Add(Convert.ToString(sizeX) + " " + Convert.ToString(sizeY));
            for (int i = 0; i < sizeX; i++)
                for (int j = 0; j < sizeY; j++)
                    lines.Add(Convert.ToString(i) + " " + Convert.ToString(j) + " " + HexConverter(color[i, j]));
            if (toTXT.ShowDialog() == DialogResult.OK)
            {
                using (StreamWriter outputFile = new StreamWriter(toTXT.OpenFile()))
                {
                    foreach (string line in lines)
                        outputFile.WriteLine(line);
                }
            }
        }

        //basic monte carlo
        private void Btn_MCInsert_Click(object sender, EventArgs e)
        {
            //add colors to the list
            //List<Color> cs = new List<Color>();
            for (int g = 0; g < tb_MCStates.Value; g++)
            {
                do randomColor = Color.FromArgb(_rand.Next(256), _rand.Next(256), _rand.Next(256));
                while (randomColor == defaultColor && randomColor == inclusionColor && eachColor.Contains(randomColor) && forbiddenColors.Contains(randomColor));
                eachColor.Add(randomColor);
            }
            int rand;
            for(int i = 0; i< sizeX; i++)
                for(int j = 0; j< sizeY; j++)
                {
                    if(!state[i,j])
                    {
                        rand = _rand.Next(0, Convert.ToInt32(tb_MCStates.Value));
                        nextcolor[i, j] = eachColor[rand];
                        nextstate[i, j] = true;
                    }
                    //allPoints.Add(new Point(i,j));
                }
            WriteBoard();
        }
        private void Btn_MCStartStop_Click(object sender, EventArgs e)
        {
            if (isActive)
                isActive = false;
            else
                isActive = true;
            mcThread = new Thread(MCRun);
            mcThread.Start();
        }
        private void SingleCell()
        { 
            //init data
            int randPt, randC;
            double preEnergy = 0;
            double preDelta = 0;
            double postEnergy = 0;
            double postDelta = 0;
            double energyDelta = 0;
            if(mcCurrentStep < Convert.ToInt32(tb_MCSteps.Value))
            {
                if (allPoints.Count > 0)
                {
                    randPt = _rand.Next(0, allPoints.Count);
                    if (!forbiddenColors.Contains(nextcolor[allPoints[randPt].X, allPoints[randPt].Y]))
                    {
                    FindBorderNeighbor(mooreNeigh, allPoints[randPt].X, allPoints[randPt].Y, true); // get tempColors
                        foreach (Color c in tempColors) //calc pre-change delta
                        {
                            if (c != color[allPoints[randPt].X, allPoints[randPt].Y])
                                preDelta++;
                        }
                        preEnergy = Convert.ToDouble(tb_MCEnergy.Value) * preDelta; //calc pre-change energy

                    //do
                    //{
                        randC = _rand.Next(0, Convert.ToInt32(eachColor.Count));
                    //}
                    //while (forbiddenColors.Contains(eachColor[randC]));  //get random color from available states

                        foreach (Color c in tempColors) //calc post-change delta
                        {
                            if (c != eachColor[randC])
                                postDelta++;
                        }
                        postEnergy = Convert.ToDouble(tb_MCEnergy.Value) * postDelta; //calc post-change energy
                        energyDelta = postEnergy - preEnergy; //calc delta
                        if (energyDelta <= 0) //probability
                            nextcolor[allPoints[randPt].X, allPoints[randPt].Y] = eachColor[randC];

                        preDelta = 0;
                        preEnergy = 0;
                        postDelta = 0;
                        postEnergy = 0;
                        tempColors.Clear();
                    }
                    allPoints.RemoveAt(randPt);
                    //WriteBoard();
                    //mcCheckedCells++;
                    //lab_MCCheckedCells.Text = "Checked cells: " + Convert.ToString(mcCheckedCells);
                }
                else
                {
                    GenerateNewPoints();
                    WriteBoard();
                    mcCurrentStep++;
                    lab_MCCurrentStep.Text = "Finished steps: " + Convert.ToString(mcCurrentStep);
                }
            }
            else
            {
                isActive = false;
            } 
        }
        private void MCRun()
        {
            while (isActive)
                SingleCell();
        }

        //misc
        private void ExitToolStripMenuItem_Click(object sender, EventArgs e) 
        {
            System.Windows.Forms.Application.Exit();
        }
        private void WriteBoard() 
        {
            for (int i = 0; i < sizeX; i++)
                for (int j = 0; j < sizeY; j++)
                {
                    state[i, j] = nextstate[i, j];
                    color[i, j] = nextcolor[i, j];
                    bmp.SetPixel(i, j, color[i, j]);
                }
            DrawingBoard.Image = bmp;
        }
        private void ClearBoard() 
        {
            for (int i = 0; i < sizeX; i++)
                for (int j = 0; j < sizeY; j++)
                {
                    state[i, j] = nextstate[i, j] = false;
                    color[i, j] = nextcolor[i, j] = defaultColor;
                    spot[i, j] = 0;
                    bmp.SetPixel(i, j, color[i, j]);
                }
            DrawingBoard.Image = bmp;
        }
        private void GenerateNewPoints()
        {
            for (int i = 0; i < sizeX; i++)
                for (int j = 0; j < sizeY; j++)
                    allPoints.Add(new Point(i, j));
        }
        private String HexConverter(Color c)
        {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }
        private bool CheckDisableControlButtons()
        {
            if (!state.Cast<bool>().Contains(false))
            {
                isFinished = true;
                Btn_StartStop_Click(null, null);
                return true;
            }
            else
                return false;
        }
        

        //unused
        private int Mod(int x, int m)
        {
            return ((x % m) + m) % m;
        }
        private void CheckEnableControlButtons()
        {
            if (!state.Cast<bool>().Contains(true))
            {
                btn_Next.Enabled = true;
                btn_StartStop.Enabled = true;
                btn_Reset.Enabled = true;

            }
        }
        //private void SaveColorFromBoard(Color c)
        //{
        //    if (c != defaultColor)
        //    {
        //        for (int i = lb_eachColor.Items.Count - 1; i >= 0; --i)
        //            if (lb_eachColor.Items[i].Text == HexConverter(c))
        //            {
        //                if (!lb_safeColors.Items.Contains(new ListViewItem(HexConverter(c))))
        //                {
        //                    lb_safeColors.Items.Add(new ListViewItem(HexConverter(c)));
        //                    lb_safeColors.Items[lb_safeColors.Items.Count - 1].BackColor = ColorTranslator.FromHtml(HexConverter(c));
        //                }
        //            }
        //        if (!safeColors.Contains(c))
        //            safeColors.Add(c);
        //        spotAmount++; //each saved grain is saved
        //        for (int i = 0; i < sizeX; i++)
        //            for (int j = 0; j < sizeY; j++)
        //            {
        //                if (color[i, j] == c)
        //                    spot[i, j] = spotAmount;
        //            }
        //    }
        //}
        //private void Lb_eachColor_DoubleClick(object sender, EventArgs e)
        //{
        //    if (rb_MicroSubs.Checked || rb_MicroDual.Checked)
        //    {
        //        DeleteColorFromBoard(ColorTranslator.FromHtml(lb_eachColor.SelectedItems[0].Text));
        //        WriteBoard();
        //        else if (rb_ClickActionSave.Checked)
        //         {
        //             SaveColorFromBoard(ColorTranslator.FromHtml(lb_eachColor.SelectedItems[0].Text));
        //             WriteBoard();
        //         }
        //    }

        //}
    }
}
