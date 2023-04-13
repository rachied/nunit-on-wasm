namespace TestProject1 
{
    public class RoboBar
    {
        public string GetGreeting(int age)
        {
            if (StrykerVTLhSNri4Y3iaEc.MutantControl.IsActive(0))
            {
                
            }
            else        
            {
                if ((StrykerVTLhSNri4Y3iaEc.MutantControl.IsActive(3)
                        ? !(age > 18)
                        : (StrykerVTLhSNri4Y3iaEc.MutantControl.IsActive(2)
                            ? age >= 18
                            : (StrykerVTLhSNri4Y3iaEc.MutantControl.IsActive(1)
                                ? age < 18
                                : age > 18))))
                {if(StrykerVTLhSNri4Y3iaEc.MutantControl.IsActive(4)){}else            {
                        return (StrykerVTLhSNri4Y3iaEc.MutantControl.IsActive(5)?"":"Here have a beer!");
                    }
                }
                return (StrykerVTLhSNri4Y3iaEc.MutantControl.IsActive(6)?"":"Sorry not today!");
            }
            return default(string);}    }
}