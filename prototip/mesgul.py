# Tek iş parçacığını N saniye %100 meşgul eder. Kullanım: mesgul.py <saniye>
import sys, time
bitis = time.time() + float(sys.argv[1])
while time.time() < bitis:
    pass
