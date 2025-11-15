namespace Roadfn.ViewModel
{
    public class UserViewModel
    {

        public string Title { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string UserName { get; set; }

        public string UserPassword { get; set; }

        public string MobileNo1 { get; set; }

        public string MobileNo2 { get; set; }


        public string Address { get; set; }

        public string Tel { get; set; }


    }
    public class UserViewModelM
    {

        public string Title { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string UserName { get; set; }

        public string UserPassword { get; set; }

        public string MobileNo1 { get; set; }

        public string MobileNo2 { get; set; }


        public string Address { get; set; }

        public string Tel { get; set; }
        public List<int> RoleIds { get; set; } = new List<int>();
        public List<int> Statuses { get; set; } = new List<int>();


    }
}
