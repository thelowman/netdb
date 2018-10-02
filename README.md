# netdb
Helpful base classes for connecting to different databases from .NET applications.

This first base class is for MS SQL.  I've used this version for many years and it has performed pretty well for me.  An ODBC version is in the works and I'll get it incorporated at some point.


## Usage
To use the netdb.Sql class, create a derived class in your project:

```c#
public class MyDB : netdb.Sql
{
  protected override string ConnectionString
  {
    get
    {
      return "Data Source=MACHINE\\INSTANCE;Initial Catalog=DBName;User ID=user; password=pass";
    }
  }
  protected MyDB() : base() { }
  public static MyDB GetData(string query)
  {
    MyDB db = new MyDB();
    db.Open(query);
    return db;
  }
  public static string[] ExecuteStatic(string query)
  {
    MyDB db = new MyDB();
    return db.Execute(query);
  }
}
```

Then use the new class in your code.

To read data:
```c#
string sql = "SELECT * FROM users";
MyDB db = MyDB.GetData(sql);
while(db.Read())
{
  /* Load up variables with your data. One iteration per row. */
}
db.Dispose();
```

To execute a command:
```c#
string sql = "INSERT INTO users (name, age) VALUES (N'Bob', 28)"
MyDB.ExecuteStatic(sql);
```

### Putting values into SQL
The SQL query is just a string so it's completely under your control however, there is an SqlQualify method with several overloads to help with writing one.

* SqlQualify(int value)
* SqlQualify(double value)
* SqlQualify(float value)
* SqlQualify(string value)
* SqlQualify(bool value)
* SqlQualify(DateTime value)
* SqlQualify(object value)
* SqlQualify(int value, bool nullIfZero)

The SqlQualify(object value) will take a shot at properly converting the object to SQL but it's limited in scope.  If the object passed is not a string, boolean or DateTime then it will be converted to a string so it's only useful if you know your data is one of these basic types but you just don't know which one.

One last SqlQualify method is SqlQualify(int value, bool nullIfZero).  Under certain circumstances such as an optional foreign key it is preferable to use dbNull instead of zero when inputting a value.  Use this method to replace 0 with NULL.

### Getting values from records

The underlying data reader is available as MyDB.Reader so you are free to leverage it in any way you need.  In most cases, one of the following methods usually does the job.

* GetString(field name or index)
* GetInt(field name or index)
* GetDecimal(field name or index)
* GetDouble(field name or index)
* GetBoolean(field name or index)
* GetDateTime(field name or index)
