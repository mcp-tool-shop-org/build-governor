#include <iostream>
#include <map>
#include <string>

void module_b() {
    std::map<std::string, int> data;
    data["alpha"] = 1;
    data["beta"] = 2;
    data["gamma"] = 3;
    std::cout << "Module B: entries = " << data.size() << std::endl;
}
