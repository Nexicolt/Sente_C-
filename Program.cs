using System.Xml.Linq;

namespace Sente4
{

    internal class Uczestnik
    {

        /// <summary>
        /// Identyfiaktor 
        /// </summary>
        public uint Id { get; set; }

        /// <summary>
        /// Poziom w piramidzie
        /// </summary>
        public uint Level { get; set; }

        public uint? DirectParentId { get; set; }

        /// <summary>
        /// Lista bezpośrednich dzieci, w jednym poziomie
        /// </summary>
        public HashSet<uint> SubordinateList { get; set; }

        /// <summary>
        /// Prowizja (zaokrąglane, więc nie ntrzeba decimal)
        /// </summary>
        public decimal Provision { get; set; }

        public override string ToString()
        {
            return $"{Id} {Level} {GetSingleCHilds()} {Provision}";
        }

        /// <summary>
        /// Zwraca listę podwłądnych, którzy nie mają swoich podwładnych
        /// </summary>
        /// <param name="startFrom"></param>
        /// <returns></returns>
        private uint GetSingleCHilds(uint startFrom = 0)
        {
            uint result = startFrom;
            foreach (var child in SubordinateList)
            {
                var element = Program.employees[child];
                if(element.SubordinateList.Count() == 0)
                {
                    result++;
                }
                else
                {
                    result += element.GetSingleCHilds();
                }
            }
            return result;
        }

    }
    internal class Program
    {
        //Słownik z uczestnikami piramidy
        public static Dictionary<uint, Uczestnik> employees = new Dictionary<uint, Uczestnik>();


        static void Main(string[] args)
        {
            if (!File.Exists("piramida.xml"))
            {
                Console.WriteLine("Brak pliku \"piramida.xml\". Operacja anulowana!");
                return;
            }
            if (!File.Exists("przelewy.xml"))
            {
                Console.WriteLine("Brak pliku \"przelewy.xml\". Operacja anulowana!");
                return;
            }

            XDocument piramidaXml = new XDocument();
            try
            {
                piramidaXml = XDocument.Load("piramida.xml");
            }catch(Exception ex)
            {
                Console.WriteLine($"Błąd parsowania pliku \"piramida.xml\". Kod błędu:\n\n" +
                    $"{ex.Message} ");
                return;
            }

            XDocument przelewyXml = new XDocument();
            try
            {
                przelewyXml = XDocument.Load("przelewy.xml");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd parsowania pliku \"przelewy.xml\". Kod błędu:\n\n" +
                    $"{ex.Message} ");
                return;
            }

            

            try
            {
                //Parsowanie uczestników
                ParseXElement(piramidaXml.Elements().First());

                //Parsowanie wpłat
                CalculateProvision(przelewyXml.Elements().First());
            }catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }
           

            //Wyświetlenie rekordów
            foreach (var employ in employees.Values)
            {
                Console.WriteLine(employ.ToString());
            }

        }

        /// <summary>
        /// Oblicza prowizję dla wszystkich pracowników
        /// </summary>
        /// <param name="xmlElement"></param>
        static private void CalculateProvision(XElement xmlElement)
        {
            uint elementNo = 1;
            foreach (var element in xmlElement.Elements())
            {
                //ID płacącego
                var payerId = element.Attribute("od");
                if (payerId == null)
                {
                    Console.WriteLine("Płatnik nie posiada identyfikatora, w danych z przelewów. Nie został on uwzględniony na liście. Nr. obiektu: " + elementNo++);
                    continue;
                }
                var paymentFromId = uint.Parse(payerId.Value);

                var paymnetValue = element.Attribute("kwota");
                if(paymnetValue == null)
                {
                    Console.WriteLine("Przelew nie posiada kwoty. Nie zostanie on uwzględniony na liście. Nr. obiektu: " + elementNo++);
                    continue;
                }
                //Kwota
                var paymentValue = decimal.Parse(paymnetValue.Value);

                AddProvisionToEmployee(paymentFromId, paymentValue);
            }
        }

        /// <summary>
        /// Rekurencyjnie idzie w góre drzewa, od wpłacającego aż do właściciela i buduje listę id przełożonych
        /// </summary>
        /// <param name="workerId"></param>
        /// <param name="actualParrents"></param>
        static private void BuildParentsList(uint workerId, ref List<uint> actualParrents)
        {
            if (!employees.ContainsKey(workerId))
            {
                throw new Exception($"W słowniku brak płatnika, o indetyfikatorze {workerId}. Operacja anulowana!");
            }
            var worker = employees[workerId]; //Wpłacający

            if (worker.DirectParentId != null)
            {
                    actualParrents.Add((uint)worker.DirectParentId);
                    BuildParentsList((uint)worker.DirectParentId, ref actualParrents);
            }

        }

        /// <summary>
        /// Rekurencyjnie dodaje prowizję po wpłacie
        /// </summary>
        /// <param name="employeId"></param>
        /// <param name="paymentValue"></param>
        static private void AddProvisionToEmployee(uint employeId, decimal paymentValue)
        {
           
            //Buduje liste rodziców i zachowuje ID w kolejności malejącej, żeby póxniej znów nie iterować bez potrzeby
            List<uint> workerParents = new List<uint>();
            BuildParentsList(employeId, ref workerParents);

            //Założyciel
            if (workerParents.Count() == 0)
            {
                employees[employeId].Provision += paymentValue;
            }

            //Iteruj po rodzicach, zaczynając od ostatniego w kolejności, czyli właściciela
            decimal cashForWorker;
            for (int i = (workerParents.Count-1); i>=0; i--)
            {
               

                //Ostatni, czyli bezpośredni przełożony wpłacającego. Nie można wtedy dzielić
                if (i == 0){
                    employees[workerParents[i]].Provision += paymentValue;
                }
                else
                {
                    cashForWorker = Math.Floor(paymentValue / 2);
                    paymentValue = Math.Ceiling(paymentValue / 2);
                    employees[workerParents[i]].Provision += cashForWorker;
                }
            }

        }

        /// <summary>
        /// Parsuje piramidę i tworzy obiekty pracowników
        /// </summary>
        /// <param name="xmlElement"></param>
        /// <param name="parentId"></param>
        /// <param name="level"></param>
        static private void ParseXElement(XElement xmlElement, uint? parentId = null, uint level=0)
        {

            foreach (var element in xmlElement.Elements())
            {

                //Dodaj gościa do słownika
                var attrId = element.Attribute("id");
                if(attrId == null)
                {
                    throw new Exception("Uczestnik nie posiada identyfikatora. Operacja anulowana!");
                }

                var elementId = uint.Parse(attrId.Value);
                AddEmployeeToDictionary(elementId, level, parentId);

                bool haveChilds = element.Elements().Count() > 0;

                //Dodaj do obiektu, rodzica do lista (optymalizacja pod póxniejsze prowizje) 
                if(parentId != null)
                {
                    employees[(uint)parentId].SubordinateList.Add(elementId);
                }

                //Jesli ma dzieci, to wywołuj sie rekurencyjnie
                if (haveChilds)
                {
                    ParseXElement(element, elementId, (level+1));
                }
            }

        }

        /// <summary>
        /// Dodaj pracownika do słownika
        /// </summary>
        /// <param name="id"></param>
        /// <param name="level"></param>
        /// <param name="parentId"></param>
        private static void AddEmployeeToDictionary(uint id, uint level, uint? parentId)
        {
            if (employees.ContainsKey(id))
            {
                throw new Exception($"Zdublowano uczestnika o identyfikatorze '{id}'. Operacja anulowana!");
            }
            employees.Add(id, new Uczestnik { Id=id, Level=level, Provision=0, DirectParentId=parentId, SubordinateList=new HashSet<uint>()});
        }
    }
}
